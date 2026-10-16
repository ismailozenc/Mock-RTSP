using System.Diagnostics;

namespace MockRtsp;

public sealed class MockCamera : IDisposable
{
    private readonly MediaMtxServer _server;
    private readonly string _path;
    private readonly string _sourceFile;
    private readonly string _ffmpegPath;
    private Process? _publisher;

    /// <param name="path">Stream path, e.g. "Streaming/Channels/101" to match Hikvision URLs.</param>
    /// <param name="sourceFile">H.264/H.265 video file to loop. Use real footage from your cameras.</param>
    public MockCamera(MediaMtxServer server, string path, string sourceFile, string ffmpegPath = "ffmpeg")
    {
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException("Source video not found.", sourceFile);

        _server = server;
        _path = path.TrimStart('/');
        _sourceFile = sourceFile;
        _ffmpegPath = ffmpegPath;
    }

    public string Url => _server.BuildUrl(_path);

    public bool IsOnline => _publisher is { HasExited: false };

    public void Start()
    {
        if (IsOnline) return;

        var psi = new ProcessStartInfo(_ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // -stream_loop must come before -i. -re paces output at real-time speed.
        foreach (var arg in new[]
        {
            "-hide_banner", "-loglevel", "warning",
            "-re", "-stream_loop", "-1",
            "-fflags", "+genpts",
            "-i", _sourceFile,
            "-an",                      // drop audio
            "-c:v", "copy",             // pass the codec through unchanged
            "-f", "rtsp",
            "-rtsp_transport", "tcp",
            Url
        })
        {
            psi.ArgumentList.Add(arg);
        }

        _publisher = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start FFmpeg. Check that it is on PATH.");
    }

    /// <summary>Stops the publisher. MediaMTX drops the stream, just as a camera going offline would.</summary>
    public void Stop()
    {
        if (_publisher is null) return;

        try
        {
            if (!_publisher.HasExited)
            {
                _publisher.Kill(entireProcessTree: true);
                _publisher.WaitForExit(3000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        finally
        {
            _publisher.Dispose();
            _publisher = null;
        }
    }

    /// <summary>Simulates a camera dropping off the network for a period, then coming back.</summary>
    public async Task SimulateOutageAsync(TimeSpan downtime, CancellationToken ct = default)
    {
        Stop();
        await Task.Delay(downtime, ct);
        Start();
    }

    public void Dispose() => Stop();
}
