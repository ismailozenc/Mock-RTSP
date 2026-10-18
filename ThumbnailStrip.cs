using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace MockRtsp;

/// <summary>
/// Small pictures of a video file at a fixed interval, for the timeline. Made in the background
/// by one FFmpeg pass that writes raw frames; they become available one by one while it runs.
/// Frames are 24-bit BGR, top row first, no padding (what a 24bpp GDI+ bitmap expects per row).
/// </summary>
public sealed class ThumbnailStrip : IDisposable
{
    private readonly object _gate = new();
    private readonly List<byte[]> _frames = new();
    private readonly CancellationTokenSource _cancel = new();
    private Process? _process;
    private bool _disposed;

    private ThumbnailStrip(int width, int height, double intervalSeconds, int expectedCount)
    {
        Width = width;
        Height = height;
        IntervalSeconds = intervalSeconds;
        ExpectedCount = expectedCount;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Seconds between thumbnails. Thumbnail i shows time i * IntervalSeconds.</summary>
    public double IntervalSeconds { get; }

    public int ExpectedCount { get; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _frames.Count;
            }
        }
    }

    public bool IsComplete { get; private set; }

    /// <summary>Why making thumbnails failed, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>The raw frame for thumbnail i, or null if it is not made yet.</summary>
    public byte[]? Get(int index)
    {
        lock (_gate)
        {
            return index >= 0 && index < _frames.Count ? _frames[index] : null;
        }
    }

    /// <summary>
    /// Starts making thumbnails in the background and returns straight away.
    /// About one per second for short videos, at most 600 for long ones.
    /// </summary>
    public static ThumbnailStrip Start(string ffmpegPath, string file, VideoInfo source, int height = 72)
    {
        double duration = Math.Max(source.DurationSeconds, 1);
        double interval = Math.Max(1, Math.Ceiling(duration / 600));
        int expected = Math.Max(1, (int)Math.Ceiling(duration / interval));

        double aspect = source.Height > 0 ? (double)source.Width / source.Height : 16.0 / 9.0;
        int width = Math.Clamp((int)Math.Round(height * aspect) & ~1, 32, 256);

        var strip = new ThumbnailStrip(width, height, interval, expected);
        _ = Task.Run(() => strip.Generate(ffmpegPath, file, duration));
        return strip;
    }

    private void Generate(string ffmpegPath, string file, double duration)
    {
        var inv = CultureInfo.InvariantCulture;
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin" })
            psi.ArgumentList.Add(arg);

        // Long videos: decode keyframes only, which is many times faster. Short ones: every frame,
        // so each thumbnail is the exact moment.
        if (duration > 300)
        {
            psi.ArgumentList.Add("-skip_frame");
            psi.ArgumentList.Add("nokey");
        }

        foreach (var arg in new[]
        {
            "-i", file, "-map", "0:v:0", "-an", "-sn", "-dn",
            "-vf", $"fps=1/{IntervalSeconds.ToString(inv)},scale={Width.ToString(inv)}:{Height.ToString(inv)}",
            "-pix_fmt", "bgr24", "-f", "rawvideo", "pipe:1"
        })
        {
            psi.ArgumentList.Add(arg);
        }

        string lastError = string.Empty;

        try
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
            }

            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lastError = e.Data.Trim();
            };
            _process.BeginErrorReadLine();

            var output = _process.StandardOutput.BaseStream;
            int frameBytes = Width * Height * 3;

            while (!_cancel.IsCancellationRequested)
            {
                var frame = new byte[frameBytes];
                if (output.ReadAtLeast(frame, frameBytes, throwOnEndOfStream: false) < frameBytes)
                    break;

                lock (_gate)
                {
                    _frames.Add(frame);
                }
            }

            _process.WaitForExit();

            if (Count == 0 && !_cancel.IsCancellationRequested)
                Error = lastError.Length > 0 ? lastError : "FFmpeg made no thumbnails.";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            if (!_cancel.IsCancellationRequested)
            {
                Error = ex.Message;
                ErrorReporter.Report(ex, $"Making thumbnails for {file}");
            }
        }
        finally
        {
            IsComplete = true;
        }
    }

    /// <summary>Stops making thumbnails. Ones already made stay available.</summary>
    public void Dispose()
    {
        Process? process;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            process = _process;
        }

        _cancel.Cancel();

        try
        {
            if (process is not null && !process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already gone.
        }
    }
}
