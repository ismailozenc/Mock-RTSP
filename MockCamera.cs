using System.ComponentModel;
using System.Diagnostics;

namespace MockRtsp;

public sealed class MockCamera : IDisposable
{
    private readonly object _gate = new();
    private readonly MediaMtxServer _server;
    private readonly string _path;
    private readonly string _sourceFile;
    private readonly string _ffmpegPath;
    private StreamOptions _options;             // Guarded by _gate. Replaced by ApplyOptions.
    private Process? _publisher;
    private bool _disposed;

    /// <summary>
    /// Raised when FFmpeg exits by itself (bad file, crash, etc.). Not raised when
    /// the camera is stopped through Stop, SimulateOutageAsync or Dispose.
    /// Raised on a thread-pool thread.
    /// </summary>
    public event EventHandler<int>? PublisherExited;

    /// <param name="path">Stream path, e.g. "Streaming/Channels/101" to match Hikvision URLs.</param>
    /// <param name="sourceFile">H.264/H.265 video file to loop. Use real footage from your cameras.</param>
    /// <param name="options">FFmpeg settings for this camera. Null uses the defaults.</param>
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
    }

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

    /// <summary>A copy of the current FFmpeg settings for this camera.</summary>
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

    /// <summary>
    /// Replaces the FFmpeg settings. A running camera is restarted so the change takes effect,
    /// which means viewers see a short drop while it reconnects.
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

    public bool IsOnline
    {
        get
        {
            lock (_gate)
            {
                return _publisher is not null && IsProcessRunning(_publisher);
            }
        }
    }

    public void Start()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_publisher is not null && IsProcessRunning(_publisher))
                return;

            // A previous publisher that exited on its own may still be around.
            _publisher?.Dispose();
            _publisher = null;

            var psi = new ProcessStartInfo(_ffmpegPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // FFmpeg must send the publisher login, or MediaMTX refuses the stream.
            string publishUrl = _server.BuildUrl(_path, _options.PublisherUser, _options.PublisherPassword);

            foreach (var arg in _options.FfmpegArguments(_sourceFile, publishUrl))
            {
                psi.ArgumentList.Add(arg);
            }

            Process process;
            try
            {
                process = Process.Start(psi)
                    ?? throw new InvalidOperationException("FFmpeg did not start.");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not start FFmpeg ('{_ffmpegPath}'). Check that ffmpeg.exe is in the ffmpeg folder.", ex);
            }

            // Assign before subscribing, so the exit handler can always recognise this process.
            _publisher = process;
            process.EnableRaisingEvents = true;
            process.Exited += (sender, _) => OnPublisherExited(sender as Process);
        }
    }

    /// <summary>Stops the publisher. MediaMTX drops the stream, just as a camera going offline would.</summary>
    public void Stop()
    {
        Process? process;
        lock (_gate)
        {
            // Clear the field first, so the exit handler ignores the process we are about to kill.
            process = _publisher;
            _publisher = null;
        }

        if (process is null) return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, $"Stopping FFmpeg for {_path}");
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>Simulates a camera dropping off the network for a period, then coming back.</summary>
    public async Task SimulateOutageAsync(TimeSpan downtime, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (downtime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(downtime), "Downtime must be positive.");

        Stop();

        try
        {
            await Task.Delay(downtime, ct);
        }
        finally
        {
            // Bring the camera back even if the wait was cancelled, unless it was disposed meanwhile.
            if (!_disposed)
                Start();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void OnPublisherExited(Process? process)
    {
        try
        {
            int exitCode;
            lock (_gate)
            {
                if (process is null || !ReferenceEquals(process, _publisher))
                    return;                         // We stopped it ourselves, or it was replaced.

                exitCode = process.ExitCode;
                _publisher = null;
                process.Dispose();
            }

            PublisherExited?.Invoke(this, exitCode);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, $"FFmpeg exit handler for {_path}");
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

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockCamera));
    }
}
