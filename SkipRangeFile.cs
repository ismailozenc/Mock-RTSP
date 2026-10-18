using System.Text.Json;

namespace MockRtsp;

/// <summary>What is saved next to a video: its skip ranges, and the motion scan if one was made.</summary>
public sealed record SavedSkips(List<SkipRange> Skips, MotionProfile? Motion, bool FileExists)
{
    /// <summary>True once the video has been scanned for motion, so it is not scanned again on load.</summary>
    public bool Scanned => Motion is not null;
}

/// <summary>
/// Keeps a video's skip ranges next to it, as "&lt;video file&gt;.skips.json" (for example
/// "parking.mp4.skips.json"), so they travel with the video and load whenever it is added.
/// The motion scan is kept in the same file, so a video is scanned only once.
/// </summary>
public static class SkipRangeFile
{
    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string PathFor(string videoPath) => videoPath + ".skips.json";

    /// <summary>
    /// Reads what is saved next to a video. No file means no ranges and no scan. Throws
    /// InvalidDataException if the file exists but cannot be read, so the user is told
    /// instead of the ranges quietly disappearing.
    /// </summary>
    public static SavedSkips Load(string videoPath, double durationSeconds)
    {
        string path = PathFor(videoPath);
        if (!File.Exists(path))
            return new SavedSkips(new List<SkipRange>(), null, false);

        try
        {
            var file = JsonSerializer.Deserialize<FileContent>(File.ReadAllText(path), JsonSettings);
            var ranges = (file?.Skips ?? new List<Entry>()).Select(e => new SkipRange(e.Start, e.End));

            MotionProfile? motion = null;
            if (file?.Motion is { Levels.Length: > 0, IntervalSeconds: > 0 } m)
                motion = new MotionProfile(m.IntervalSeconds, Convert.FromBase64String(m.Levels));

            return new SavedSkips(SkipRanges.Normalize(ranges, durationSeconds), motion, true);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a valid skip file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves the ranges (and the motion scan) next to the video. The file is written beside the
    /// old one and then swapped in, so a crash cannot leave half a file. With no ranges and no
    /// scan the file is deleted; a scanned video keeps its file even with no ranges, so it is
    /// not scanned again.
    /// </summary>
    public static void Save(string videoPath, IReadOnlyList<SkipRange> ranges, MotionProfile? motion)
    {
        string path = PathFor(videoPath);

        if (ranges.Count == 0 && motion is null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        var content = new FileContent
        {
            Video = Path.GetFileName(videoPath),
            Skips = ranges.Select(r => new Entry { Start = Math.Round(r.Start, 3), End = Math.Round(r.End, 3) }).ToList(),
            Motion = motion is null ? null : new MotionEntry
            {
                IntervalSeconds = motion.IntervalSeconds,
                Levels = Convert.ToBase64String(motion.Levels)
            }
        };

        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(content, JsonSettings));

        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }

    private sealed class FileContent
    {
        public string Video { get; set; } = string.Empty;
        public List<Entry> Skips { get; set; } = new();
        public MotionEntry? Motion { get; set; }
    }

    private sealed class Entry
    {
        public double Start { get; set; }
        public double End { get; set; }
    }

    /// <summary>Movement per sample, percent x 10, one byte each, base64.</summary>
    private sealed class MotionEntry
    {
        public double IntervalSeconds { get; set; }
        public string Levels { get; set; } = string.Empty;
    }
}
