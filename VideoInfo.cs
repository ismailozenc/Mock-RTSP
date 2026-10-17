using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MockRtsp;

/// <summary>Size, frame rate and length of a video file.</summary>
public sealed record VideoInfo(int Width, int Height, double FrameRate, double DurationSeconds)
{
    /// <summary>
    /// Reads the video's details with "ffmpeg -i", so ffprobe.exe is not needed.
    /// Throws InvalidOperationException if the file has no readable video stream.
    /// </summary>
    public static VideoInfo Probe(string ffmpegPath, string file)
    {
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-nostdin");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(file);

        string text;
        try
        {
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            text = process.StandardError.ReadToEnd();       // ffmpeg prints the details on stderr
            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException($"FFmpeg took too long to read '{file}'.");
            }
            stdout.Wait(1000);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Could not start FFmpeg ('{ffmpegPath}').", ex);
        }

        return Parse(text) ?? throw new InvalidOperationException(
            $"No playable video found in '{Path.GetFileName(file)}'.{Environment.NewLine}{LastLines(text, 3)}");
    }

    /// <summary>Parses "ffmpeg -i" output. Returns null if there is no video stream.</summary>
    public static VideoInfo? Parse(string ffmpegOutput)
    {
        var inv = CultureInfo.InvariantCulture;

        // e.g. "Stream #0:0[0x1](und): Video: h264 (High) (avc1 / 0x31637661), yuv420p(progressive), 1920x1080 [SAR 1:1 DAR 16:9], 25 fps, 25 tbr, ..."
        var video = Regex.Match(ffmpegOutput, @"Stream #\d+:\d+.*?: Video: (?<rest>.*)");
        if (!video.Success)
            return null;

        string line = video.Groups["rest"].Value;

        var size = Regex.Match(line, @", (?<w>\d{2,5})x(?<h>\d{2,5})");
        if (!size.Success)
            return null;

        double rate = 0;
        var fps = Regex.Match(line, @"(?<f>\d+(?:\.\d+)?) fps");
        var tbr = Regex.Match(line, @"(?<f>\d+(?:\.\d+)?)k? tbr");
        if (fps.Success)
            rate = double.Parse(fps.Groups["f"].Value, inv);
        else if (tbr.Success)
            rate = double.Parse(tbr.Groups["f"].Value, inv);

        double duration = 0;
        var d = Regex.Match(ffmpegOutput, @"Duration: (?<h>\d+):(?<m>\d{2}):(?<s>\d{2}(?:\.\d+)?)");
        if (d.Success)
        {
            duration = int.Parse(d.Groups["h"].Value, inv) * 3600
                       + int.Parse(d.Groups["m"].Value, inv) * 60
                       + double.Parse(d.Groups["s"].Value, inv);
        }

        return new VideoInfo(
            int.Parse(size.Groups["w"].Value, inv),
            int.Parse(size.Groups["h"].Value, inv),
            rate,
            duration);
    }

    private static string LastLines(string text, int count) =>
        string.Join(Environment.NewLine, text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(count));
}
