using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace MockRtsp;

/// <summary>
/// Measures movement through a whole video in the background: FFmpeg decodes it into small
/// grey pictures, 5 a second, and each is compared with the one before. Small differences
/// (sensor noise, compression) are ignored, so only real changes count.
/// </summary>
public sealed class MotionScan : IDisposable
{
    public const double SamplesPerSecond = 5;
    private const int Width = 160;
    private const int Height = 90;
    private const int PixelThreshold = 18;              // grey levels a pixel must change by to count

    private readonly CancellationTokenSource _cancel = new();
    private readonly double _duration;
    private Process? _process;
    private int _samples;

    private MotionScan(double durationSeconds)
    {
        _duration = durationSeconds;
    }

    /// <summary>0 to 1.</summary>
    public double Progress => _duration <= 0 ? 0 : Math.Min(1, _samples / (_duration * SamplesPerSecond));

    public bool IsComplete { get; private set; }

    public bool IsCancelled => _cancel.IsCancellationRequested;

    /// <summary>The measured movement, once complete without error.</summary>
    public MotionProfile? Result { get; private set; }

    public string? Error { get; private set; }

    public static MotionScan Start(string ffmpegPath, string file, VideoInfo source)
    {
        var scan = new MotionScan(source.DurationSeconds);
        _ = Task.Run(() => scan.Run(ffmpegPath, file));
        return scan;
    }

    private void Run(string ffmpegPath, string file)
    {
        var inv = CultureInfo.InvariantCulture;
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in new[]
        {
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-i", file, "-map", "0:v:0", "-an", "-sn", "-dn",
            // area scaling averages blocks of pixels, which also smooths away most noise
            "-vf", $"fps={SamplesPerSecond.ToString(inv)},scale={Width}:{Height}:flags=area,format=gray",
            "-f", "rawvideo", "pipe:1"
        })
        {
            psi.ArgumentList.Add(arg);
        }

        string lastError = string.Empty;
        var levels = new List<byte>();

        try
        {
            if (_cancel.IsCancellationRequested)
                return;

            _process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lastError = e.Data.Trim();
            };
            _process.BeginErrorReadLine();

            var output = _process.StandardOutput.BaseStream;
            var previous = new byte[Width * Height];
            var current = new byte[Width * Height];
            bool first = true;

            while (!_cancel.IsCancellationRequested
                   && output.ReadAtLeast(current, current.Length, throwOnEndOfStream: false) == current.Length)
            {
                levels.Add(first ? (byte)0 : Level(previous, current));
                first = false;
                (previous, current) = (current, previous);
                _samples++;
            }

            _process.WaitForExit();

            if (_cancel.IsCancellationRequested)
                return;

            if (levels.Count == 0)
                Error = lastError.Length > 0 ? lastError : "FFmpeg could not read the video.";
            else
                Result = new MotionProfile(1 / SamplesPerSecond, levels.ToArray());
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            if (!_cancel.IsCancellationRequested)
            {
                Error = ex.Message;
                ErrorReporter.Report(ex, $"Motion scan of {file}");
            }
        }
        finally
        {
            IsComplete = true;
        }
    }

    /// <summary>Share of pixels that changed by more than the noise threshold, as a level (percent x 10).</summary>
    private static byte Level(byte[] previous, byte[] current)
    {
        int changed = 0;
        for (int i = 0; i < current.Length; i++)
        {
            if (Math.Abs(current[i] - previous[i]) > PixelThreshold)
                changed++;
        }

        double percent = 100.0 * changed / current.Length;
        return (byte)Math.Min(255, Math.Round(percent * 10));
    }

    /// <summary>Stops the scan. Its result is then never set.</summary>
    public void Dispose()
    {
        _cancel.Cancel();
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already gone.
        }
    }
}
