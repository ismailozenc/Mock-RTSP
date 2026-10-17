using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MockRtsp;

/// <summary>
/// A mock camera that plays a video file into MediaMTX like a live camera, with pause,
/// seek and speed control.
///
/// It runs two FFmpeg processes with a pump thread between them:
///   decoder  reads the file from a position and outputs raw frames. Restarted to seek.
///   pump     sends exactly one frame per frame interval to the encoder. Paused, it sends the
///            last frame again; at 2x speed it takes two source frames per output frame.
///   encoder  turns the frames into H.264 and publishes them. Runs for as long as the camera is on.
/// Because the encoder never stops, viewers stay connected through pause, rewind and forward,
/// as they would with a real camera.
/// </summary>
public sealed class MockCamera : IDisposable
{
    private readonly object _gate = new();          // guards the running processes and _options
    private readonly MediaMtxServer _server;
    private readonly string _path;
    private readonly string _sourceFile;
    private readonly string _ffmpegPath;
    private readonly VideoInfo _source;
    private StreamOptions _options;
    private Process? _encoder;
    private Thread? _pump;
    private CancellationTokenSource? _stop;
    private bool _disposed;

    // Player state, shared by the UI thread and the pump.
    private readonly object _control = new();
    private double _positionSeconds;                // source position of the frame being sent
    private double? _seekTo;                        // a seek waiting for the pump
    private bool _paused;
    private double _speed = 1.0;

    // The last lines FFmpeg wrote, kept to explain why a camera stopped.
    private readonly Queue<string> _ffmpegOutput = new();
    private const int MaxOutputLines = 8;

    /// <summary>
    /// Raised when the camera stops by itself (FFmpeg failed, the encoder was refused, the file
    /// could not be read). Not raised for Stop, SimulateOutageAsync or Dispose.
    /// Raised on a background thread.
    /// </summary>
    public event EventHandler<int>? PublisherExited;

    /// <param name="path">Stream path, e.g. "Streaming/Channels/101" to match Hikvision URLs.</param>
    /// <param name="sourceFile">Video file to play. Use real footage from your cameras.</param>
    /// <param name="options">Video settings for this camera. Null uses the defaults.</param>
    public MockCamera(
        MediaMtxServer server,
        string path,
        string sourceFile,
        string ffmpegPath = "ffmpeg",
        StreamOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Stream path is required.", nameof(path));

        if (!File.Exists(sourceFile))
            throw new FileNotFoundException("Source video not found.", sourceFile);

        _options = (options ?? new StreamOptions()).Clone();

        var problems = _options.Validate();
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(options));

        _server = server;
        _path = path.Trim().TrimStart('/');
        _sourceFile = sourceFile;
        _ffmpegPath = ffmpegPath;

        // Read the size, frame rate and length up front: an unplayable file fails here, at Add.
        _source = VideoInfo.Probe(ffmpegPath, sourceFile);
    }

    // ===================================================================== information

    /// <summary>The camera's URL without login details. Safe to show in the log.</summary>
    public string Url => _server.BuildUrl(_path);

    /// <summary>The URL a viewer uses, including the viewer login when one is set.</summary>
    public string ViewerUrl
    {
        get
        {
            lock (_gate)
            {
                return _server.BuildUrl(_path, _options.ViewerUser, _options.ViewerPassword);
            }
        }
    }

    /// <summary>A copy of the current video settings for this camera.</summary>
    public StreamOptions Options
    {
        get
        {
            lock (_gate)
            {
                return _options.Clone();
            }
        }
    }

    /// <summary>The size and frame rate this camera sends.</summary>
    public VideoFormat Format
    {
        get
        {
            lock (_gate)
            {
                return _options.OutputFormat(_source);
            }
        }
    }

    /// <summary>The length of the video file. Zero if FFmpeg did not report it.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(_source.DurationSeconds);

    /// <summary>Where in the file the picture being sent comes from.</summary>
    public TimeSpan Position
    {
        get
        {
            lock (_control)
            {
                return TimeSpan.FromSeconds(_positionSeconds);
            }
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_control)
            {
                return _paused;
            }
        }
    }

    /// <summary>Playback speed, 0.1 to 8. 1 is normal speed.</summary>
    public double Speed
    {
        get
        {
            lock (_control)
            {
                return _speed;
            }
        }
        set
        {
            lock (_control)
            {
                _speed = Math.Clamp(value, 0.1, 8.0);
            }
        }
    }

    /// <summary>The last lines FFmpeg printed, for showing why it stopped. Empty if it printed nothing.</summary>
    public string LastFfmpegOutput
    {
        get
        {
            lock (_ffmpegOutput)
            {
                return string.Join(Environment.NewLine, _ffmpegOutput);
            }
        }
    }

    public bool IsOnline
    {
        get
        {
            lock (_gate)
            {
                return _pump is { IsAlive: true } && _encoder is not null && IsProcessRunning(_encoder);
            }
        }
    }

    // ===================================================================== player controls

    /// <summary>Freezes the picture. Viewers stay connected and keep receiving the same frame.</summary>
    public void Pause()
    {
        lock (_control)
        {
            _paused = true;
        }
    }

    public void Resume()
    {
        lock (_control)
        {
            // At the end of a video that does not loop, play it again from the start.
            if (_source.DurationSeconds > 0 && _positionSeconds >= _source.DurationSeconds - 0.5)
            {
                _seekTo = 0;
                _positionSeconds = 0;
            }

            _paused = false;
        }
    }

    /// <summary>Jumps to a position in the file. Works while paused, and while stopped (the camera starts there).</summary>
    public void SeekTo(TimeSpan position)
    {
        double seconds = Math.Max(0, position.TotalSeconds);

        if (_source.DurationSeconds > 0)
            seconds = Math.Min(seconds, Math.Max(0, _source.DurationSeconds - 0.25));

        lock (_control)
        {
            _seekTo = seconds;
            _positionSeconds = seconds;
        }
    }

    /// <summary>Jumps forward (positive) or back (negative) from the current position.</summary>
    public void SeekBy(TimeSpan offset) => SeekTo(Position + offset);

    // ===================================================================== settings

    /// <summary>
    /// Replaces the video settings. A running camera restarts so the change takes effect,
    /// which disconnects viewers, as a real camera does when its resolution changes.
    /// Position, pause and speed are kept.
    /// </summary>
    public void ApplyOptions(StreamOptions options)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);

        var copy = options.Clone();
        var problems = copy.Validate();
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(options));

        bool wasRunning = IsOnline;

        Stop();

        lock (_gate)
        {
            _options = copy;
        }

        if (wasRunning)
            Start();
    }

    /// <summary>
    /// Waits until a viewer can actually play the stream. Returns false if the camera stops or
    /// the stream is not up within the timeout.
    /// </summary>
    public async Task<bool> WaitUntilLiveAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (_disposed || !IsOnline)
                return false;

            string user, password;
            lock (_gate)
            {
                user = _options.ViewerUser;
                password = _options.ViewerPassword;
            }

            if (await _server.IsPathReadyAsync(_path, user, password, ct))
                return true;

            await Task.Delay(250, ct);
        }

        return false;
    }

    // ===================================================================== start and stop

    public void Start()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_pump is { IsAlive: true })
                return;

            KillQuietly(_encoder);
            _encoder = null;

            lock (_ffmpegOutput)
            {
                _ffmpegOutput.Clear();
            }

            var format = _options.OutputFormat(_source);
            var encoder = StartEncoder(format);
            var stop = new CancellationTokenSource();

            var pump = new Thread(() => Pump(encoder, format, stop.Token))
            {
                IsBackground = true,
                Name = "MockCamera " + _path,
                Priority = ThreadPriority.AboveNormal
            };

            _encoder = encoder;
            _stop = stop;
            _pump = pump;
            pump.Start();
        }
    }

    /// <summary>Stops the camera. MediaMTX drops the stream, just as a camera going offline would.</summary>
    public void Stop()
    {
        Thread? pump;
        Process? encoder;
        CancellationTokenSource? stop;

        lock (_gate)
        {
            // Clear the fields first, so the pump treats what follows as a requested stop.
            pump = _pump;
            encoder = _encoder;
            stop = _stop;
            _pump = null;
            _encoder = null;
            _stop = null;
        }

        stop?.Cancel();
        KillQuietly(encoder);          // also unblocks a pump that is writing a frame

        if (pump is not null && pump != Thread.CurrentThread && !pump.Join(5000))
            ErrorReporter.Report(new TimeoutException("The camera thread did not stop in time."), $"Stopping {_path}");

        stop?.Dispose();
    }

    /// <summary>
    /// Simulates a camera dropping off the network for a period, then coming back. Like a real
    /// camera, it comes back showing "now": unless paused, the video has moved on by the downtime.
    /// </summary>
    public async Task SimulateOutageAsync(TimeSpan downtime, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (downtime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(downtime), "Downtime must be positive.");

        Stop();
        var stoppedAt = Stopwatch.StartNew();

        try
        {
            await Task.Delay(downtime, ct);
        }
        finally
        {
            // Bring the camera back even if the wait was cancelled, unless it was disposed meanwhile.
            if (!_disposed)
            {
                MoveOnBy(stoppedAt.Elapsed);
                Start();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    // ===================================================================== the pump

    private void Pump(Process encoder, VideoFormat format, CancellationToken stop)
    {
        using var timer = HighResolutionTimer.Begin();
        Decoder? decoder = null;

        try
        {
            var output = encoder.StandardInput.BaseStream;
            var frame = new byte[format.FrameBytes];        // the frame being sent
            var next = new byte[format.FrameBytes];

            // Start from black (in yuv420p black is Y = 0 with U and V at 128), not green.
            int lumaBytes = format.Width * format.Height;
            Array.Fill(frame, (byte)128, lumaBytes, format.FrameBytes - lumaBytes);

            bool loop;
            lock (_gate)
            {
                loop = _options.Loop;
            }

            double start;
            lock (_control)
            {
                start = _positionSeconds;
                _seekTo = null;                             // the start position already covers it
            }

            decoder = StartDecoder(start, format);
            if (ReadFrame(ref decoder, format, frame, loop))
                SetPosition(decoder!, format);

            var clock = Stopwatch.StartNew();
            long sent = 0;
            double owed = 0;                                // source frames due, for speeds other than 1x

            while (!stop.IsCancellationRequested)
            {
                double? seek;
                bool paused;
                double speed;
                lock (_control)
                {
                    seek = _seekTo;
                    _seekTo = null;
                    paused = _paused;
                    speed = _speed;
                }

                if (seek is double target)
                {
                    // Only the decoder restarts. The encoder, and every viewer, carries on.
                    decoder?.Dispose();
                    decoder = StartDecoder(target, format);
                    owed = 0;

                    // Show the new position straight away, also while paused.
                    if (ReadFrame(ref decoder, format, next, loop))
                    {
                        (frame, next) = (next, frame);
                        SetPosition(decoder!, format);
                    }
                }
                else if (!paused)
                {
                    owed += speed;
                    while (owed >= 1)
                    {
                        owed -= 1;
                        if (ReadFrame(ref decoder, format, next, loop))
                        {
                            (frame, next) = (next, frame);
                            SetPosition(decoder!, format);
                        }
                        else
                        {
                            // End of a video that does not loop: hold the last frame.
                            lock (_control)
                            {
                                _paused = true;
                            }
                            owed = 0;
                            break;
                        }
                    }
                }

                output.Write(frame, 0, frame.Length);
                sent++;

                // Frame n is due n frame-intervals after the start. If the computer stalled for
                // over a second, start a fresh schedule instead of bursting to catch up.
                double due = sent * 1000.0 / format.FrameRate;
                if (clock.Elapsed.TotalMilliseconds - due > 1000)
                {
                    clock.Restart();
                    sent = 0;
                }
                else
                {
                    WaitUntil(clock, due, stop);
                }
            }
        }
        catch (Exception ex) when (!stop.IsCancellationRequested)
        {
            OnPumpFailed(encoder, ex);
        }
        catch (Exception)
        {
            // Stop() killed the encoder while a frame was being written. Expected.
        }
        finally
        {
            decoder?.Dispose();
        }
    }

    /// <summary>
    /// Reads the next frame into the buffer. At the end of the file it starts again from the
    /// beginning when looping, otherwise returns false.
    /// </summary>
    private bool ReadFrame(ref Decoder? decoder, VideoFormat format, byte[] into, bool loop)
    {
        if (decoder is not null && decoder.TryRead(into))
            return true;

        // The decoder ended: end of file, or it failed.
        bool producedNothing = decoder is null || decoder.Frames == 0;
        double startedAt = decoder?.StartSeconds ?? 0;
        decoder?.Dispose();

        if (producedNothing && startedAt <= 0)
            throw new InvalidOperationException("FFmpeg could not decode the video file.");

        if (!loop)
            return false;

        decoder = StartDecoder(0, format);
        if (decoder.TryRead(into))
            return true;

        throw new InvalidOperationException("FFmpeg could not decode the video file from the start.");
    }

    private void SetPosition(Decoder decoder, VideoFormat format)
    {
        lock (_control)
        {
            // A seek requested meanwhile has already moved the position; keep that until it is done.
            if (_seekTo is null)
                _positionSeconds = decoder.StartSeconds + (decoder.Frames - 1) / (double)format.FrameRate;
        }
    }

    private void MoveOnBy(TimeSpan elapsed)
    {
        lock (_control)
        {
            if (_paused)
                return;

            double position = _positionSeconds + elapsed.TotalSeconds * _speed;
            double duration = _source.DurationSeconds;

            if (duration > 0)
            {
                bool loop;
                lock (_gate)
                {
                    loop = _options.Loop;
                }

                position = loop ? position % duration : Math.Min(position, Math.Max(0, duration - 0.25));
            }

            _positionSeconds = position;
        }
    }

    private static void WaitUntil(Stopwatch clock, double dueMilliseconds, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            double remaining = dueMilliseconds - clock.Elapsed.TotalMilliseconds;
            if (remaining <= 0)
                return;

            if (remaining > 2)
                Thread.Sleep((int)(remaining - 1));
            else
                Thread.SpinWait(100);
        }
    }

    private void OnPumpFailed(Process encoder, Exception ex)
    {
        try
        {
            int exitCode = -1;

            if (ex is IOException)
            {
                // The encoder went away (for example MediaMTX refused the stream). Let its last
                // output arrive, because that says why.
                try
                {
                    if (encoder.WaitForExit(2000))
                    {
                        encoder.WaitForExit();
                        exitCode = encoder.ExitCode;
                    }
                }
                catch (Exception waitError) when (waitError is InvalidOperationException or Win32Exception)
                {
                }
            }
            else
            {
                Remember("camera", ex.Message);
            }

            lock (_gate)
            {
                if (!ReferenceEquals(_encoder, encoder))
                    return;                                 // stopped or replaced meanwhile

                _encoder = null;
                _pump = null;
                _stop = null;
            }

            KillQuietly(encoder);
            PublisherExited?.Invoke(this, exitCode);
        }
        catch (Exception inner)
        {
            ErrorReporter.Report(inner, $"Camera {_path} stop handler");
        }
    }

    // ===================================================================== FFmpeg processes

    private Process StartEncoder(VideoFormat format)
    {
        // FFmpeg must send the publisher login, or MediaMTX refuses the stream.
        string publishUrl = _server.BuildUrl(_path, _options.PublisherUser, _options.PublisherPassword);

        var psi = new ProcessStartInfo(_ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };

        foreach (var arg in _options.EncoderArguments(format, publishUrl))
            psi.ArgumentList.Add(arg);

        var process = StartProcess(psi);
        process.ErrorDataReceived += (_, e) => Remember("encoder", e.Data);
        process.BeginErrorReadLine();
        return process;
    }

    private Decoder StartDecoder(double startSeconds, VideoFormat format)
    {
        var psi = new ProcessStartInfo(_ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in StreamOptions.DecoderArguments(_sourceFile, startSeconds, format))
            psi.ArgumentList.Add(arg);

        var process = StartProcess(psi);
        process.ErrorDataReceived += (_, e) => Remember("decoder", e.Data);
        process.BeginErrorReadLine();
        return new Decoder(process, startSeconds);
    }

    private Process StartProcess(ProcessStartInfo psi)
    {
        try
        {
            return Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start FFmpeg ('{_ffmpegPath}'). Check that ffmpeg.exe is in the ffmpeg folder.", ex);
        }
    }

    private void Remember(string source, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (_ffmpegOutput)
        {
            _ffmpegOutput.Enqueue($"{source}: {line.Trim()}");
            while (_ffmpegOutput.Count > MaxOutputLines)
                _ffmpegOutput.Dequeue();
        }
    }

    private static bool IsProcessRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private static void KillQuietly(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already gone.
        }
        finally
        {
            process.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockCamera));
    }

    /// <summary>One decoder process, reading the file from StartSeconds.</summary>
    private sealed class Decoder : IDisposable
    {
        private readonly Process _process;
        private readonly Stream _output;
        private bool _disposed;

        public Decoder(Process process, double startSeconds)
        {
            _process = process;
            _output = process.StandardOutput.BaseStream;
            StartSeconds = startSeconds;
        }

        public double StartSeconds { get; }

        /// <summary>Frames read so far.</summary>
        public int Frames { get; private set; }

        /// <summary>Reads one whole frame. False at the end of the output.</summary>
        public bool TryRead(byte[] buffer)
        {
            if (_disposed)
                return false;

            int read = _output.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            if (read < buffer.Length)
                return false;

            Frames++;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            KillQuietly(_process);
        }
    }

    /// <summary>
    /// Windows wakes sleeping threads every 15.6 ms by default, too coarse to send frames on
    /// time. This asks for 1 ms while a camera runs.
    /// </summary>
    private sealed class HighResolutionTimer : IDisposable
    {
        private bool _active;

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint milliseconds);

        public static HighResolutionTimer Begin()
        {
            var timer = new HighResolutionTimer();

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    timer._active = timeBeginPeriod(1) == 0;
                }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
                {
                }
            }

            return timer;
        }

        public void Dispose()
        {
            if (!_active) return;
            _active = false;
            timeEndPeriod(1);
        }
    }
}
