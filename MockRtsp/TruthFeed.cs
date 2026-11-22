using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MockRtsp;

/// <summary>
/// The ground-truth feed: a TCP server on the RTSP port + 1 (8555 next to 8554) that tells
/// connected programs, live, what the cameras are streaming: which playing part each camera has
/// just entered and the vehicles labelled in it, each labelled vehicle (direction and plate) at the
/// moment its frame is streamed, and each ground-truth plate likewise. An ALPR or vehicle-detection
/// program (for example Birel Plaka) can compare what it detects with this.
///
/// Protocol: plain TCP, UTF-8, one JSON object per line ("\n"), camelCase. On connecting, a client
/// gets a "hello" (the cameras). It may send lines too:
///   camera &lt;stream path&gt;   only messages of this camera from now on (several lines: several cameras),
///                           starting with the part it is in now
///   all                     every camera again (the default), starting with the part each is in now
/// A client that sends nothing within half a second gets every camera, with the part each is in now.
/// Messages ("type"): hello, camera (added/removed), part, vehicle, plate; see the README.
/// The server only sends; a client too slow to keep up (over 2000 lines behind) is disconnected.
/// Thread-safe: Publish is called from the cameras' threads.
/// </summary>
public sealed class TruthFeed : IDisposable
{
    public const int ProtocolVersion = 1;
    private const int MaxQueuedLines = 2000;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly TcpListener _listener;
    private readonly Func<IReadOnlyList<object>> _cameras;
    private readonly ConcurrentDictionary<Client, byte> _clients = new();
    private readonly ConcurrentDictionary<string, string> _currentParts = new(StringComparer.OrdinalIgnoreCase);   // camera → its last "part" line
    private readonly CancellationTokenSource _stop = new();
    private readonly Action<string> _log;

    /// <summary>Starts listening on all network interfaces. Throws InvalidOperationException when the port is taken.</summary>
    /// <param name="cameras">The cameras for "hello" (camera and substream path each).</param>
    public TruthFeed(int port, Func<IReadOnlyList<object>> cameras, Action<string> log)
    {
        Port = port;
        _cameras = cameras;
        _log = log;
        _listener = new TcpListener(IPAddress.Any, port);
        try
        {
            _listener.Start();
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"The ground-truth feed port {port} (the RTSP port + 1) is already in use by another program. Free it or choose another RTSP port.", ex);
        }

        _ = AcceptAsync();
    }

    public int Port { get; }

    public int ClientCount => _clients.Count;

    // ===================================================================== messages

    /// <summary>A camera was added or removed (state "added" / "removed").</summary>
    public void CameraChanged(string camera, string? substream, string state)
    {
        if (state == "removed")
            _currentParts.TryRemove(camera, out _);
        Publish(camera, new { type = "camera", utc = Utc(DateTime.UtcNow), camera, substream, state });
    }

    /// <summary>A camera entered a playing part.</summary>
    public void Part(string camera, PartStreamedEventArgs e)
    {
        string line = Line(new
        {
            type = "part",
            utc = Utc(e.Utc),
            camera,
            video = Path.GetFileName(e.Video.File),
            videoTime = e.At,
            part = e.Part.Number,
            partStart = Math.Round(e.Part.Start, 3),
            partEnd = Math.Round(e.Part.End, 3),
            labelled = e.Vehicles.Count > 0 || e.MarkedEmpty,
            noVehicles = e.MarkedEmpty,
            vehicles = e.Vehicles.Select(v => new { time = Math.Round(v.Time, 3), direction = v.DirectionText, plate = v.Plate }).ToArray()
        });
        _currentParts[camera] = line;
        Send(camera, line);
    }

    /// <summary>A labelled vehicle's moment was streamed.</summary>
    public void Vehicle(string camera, VehicleStreamedEventArgs e) =>
        Publish(camera, new
        {
            type = "vehicle",
            utc = Utc(e.Utc),
            camera,
            video = Path.GetFileName(e.Video.File),
            videoTime = Math.Round(e.Vehicle.Time, 3),
            part = e.Part.Number,
            direction = e.Vehicle.DirectionText,
            plate = e.Vehicle.Plate
        });

    /// <summary>A ground-truth plate was streamed (the same moment as the ground-truth log).</summary>
    public void Plate(string camera, PlateStreamedEventArgs e) =>
        Publish(camera, new
        {
            type = "plate",
            utc = Utc(e.Utc),
            camera,
            video = Path.GetFileName(e.Video.File),
            videoTime = Math.Round(e.Plate.Time, 3),
            plate = e.Plate.Plate,
            region = e.Plate.HasRegion ? new { x = e.Plate.X, y = e.Plate.Y, width = e.Plate.Width, height = e.Plate.Height } : null
        });

    private void Publish(string camera, object message) => Send(camera, Line(message));

    private void Send(string camera, string line)
    {
        foreach (var client in _clients.Keys)
        {
            if (client.Wants(camera))
                client.Enqueue(line);
        }
    }

    private static string Line(object message) => JsonSerializer.Serialize(message, Json);

    private static string Utc(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);

    // ===================================================================== connections

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient tcp;
            try
            {
                tcp = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                if (!_stop.IsCancellationRequested)
                    ErrorReporter.Report(ex, "Ground-truth feed: accepting a connection");
                return;
            }

            var client = new Client(this, tcp);
            _clients[client] = 0;
            _log($"Ground-truth feed: {client.Name} connected ({_clients.Count} connected).");

            client.Enqueue(Line(new { type = "hello", protocol = ProtocolVersion, app = "Mock RTSP", utc = Utc(DateTime.UtcNow), cameras = _cameras() }));
            _ = client.RunAsync(_stop.Token);
            _ = GreetAsync(client);
        }
    }

    /// <summary>A client that asked for nothing within half a second watches every camera: their current parts.</summary>
    private async Task GreetAsync(Client client)
    {
        try
        {
            await Task.Delay(500, _stop.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (client.Greet())
            SendCurrentParts(client);
    }

    /// <summary>The part each camera the client watches is in now.</summary>
    private void SendCurrentParts(Client client)
    {
        foreach (var (camera, part) in _currentParts)
        {
            if (client.Wants(camera))
                client.Enqueue(part);
        }
    }

    private void Remove(Client client, string why)
    {
        if (_clients.TryRemove(client, out _))
            _log($"Ground-truth feed: {client.Name} disconnected ({why}; {_clients.Count} connected).");
    }

    /// <summary>A client's subscription line: "camera &lt;path&gt;" or "all".</summary>
    private void OnLine(Client client, string line)
    {
        line = line.Trim();
        if (line.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            client.WatchAll();
            client.Greet();
            SendCurrentParts(client);
            return;
        }

        if (line.StartsWith("camera ", StringComparison.OrdinalIgnoreCase))
        {
            string camera = line.Substring("camera ".Length).Trim().Trim('/');
            client.Watch(camera);
            client.Greet();
            if (_currentParts.TryGetValue(camera, out var part))
                client.Enqueue(part);
            return;
        }

        if (line.Length > 0)
            client.Enqueue(Line(new { type = "error", message = $"Unknown request '{line}'. Send 'camera <stream path>' or 'all'." }));
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        foreach (var client in _clients.Keys)
            client.Close();
        _clients.Clear();
    }

    /// <summary>One connected program: a queue of lines written by its own task, and its camera filter.</summary>
    private sealed class Client
    {
        private readonly TruthFeed _feed;
        private readonly TcpClient _tcp;
        private readonly BlockingCollection<string> _lines = new(MaxQueuedLines);
        private readonly HashSet<string> _cameras = new(StringComparer.OrdinalIgnoreCase);    // empty: all
        private int _greeted;                                                                  // 1 once the current parts were sent

        public Client(TruthFeed feed, TcpClient tcp)
        {
            _feed = feed;
            _tcp = tcp;
            Name = tcp.Client.RemoteEndPoint?.ToString() ?? "a client";
        }

        public string Name { get; }

        public bool Wants(string camera)
        {
            lock (_cameras)
                return _cameras.Count == 0 || _cameras.Contains(camera.Trim('/'));
        }

        public void Watch(string camera)
        {
            lock (_cameras)
                _cameras.Add(camera);
        }

        /// <summary>True the first time only: the current parts are due.</summary>
        public bool Greet() => Interlocked.Exchange(ref _greeted, 1) == 0;

        public void WatchAll()
        {
            lock (_cameras)
                _cameras.Clear();
        }

        public void Enqueue(string line)
        {
            if (!_lines.IsAddingCompleted && !_lines.TryAdd(line))
            {
                _feed.Remove(this, "too slow: over " + MaxQueuedLines + " lines behind");
                Close();
            }
        }

        public async Task RunAsync(CancellationToken stop)
        {
            var stream = _tcp.GetStream();
            var reading = ReadAsync(stream, stop);
            try
            {
                await Task.Run(() =>
                {
                    foreach (string line in _lines.GetConsumingEnumerable(stop))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }, stop);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
            {
                // Gone, stopped or closed: nothing more to send.
            }
            finally
            {
                _feed.Remove(this, "connection closed");
                Close();
            }

            await reading;
        }

        private async Task ReadAsync(NetworkStream stream, CancellationToken stop)
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                while (!stop.IsCancellationRequested && await reader.ReadLineAsync(stop) is { } line)
                    _feed.OnLine(this, line);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                // The connection closed.
            }

            // Done reading (the client sends nothing more). Sending goes on until the connection fails.
        }

        public void Close()
        {
            _lines.CompleteAdding();
            try
            {
                _tcp.Close();
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                // Already closed.
            }
        }
    }
}
