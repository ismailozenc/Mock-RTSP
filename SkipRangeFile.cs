using System.Text.Json;

namespace MockRtsp;

/// <summary>
/// Keeps a video's skip ranges next to it, as "&lt;video file&gt;.skips.json" (for example
/// "parking.mp4.skips.json"), so they travel with the video and load whenever it is added.
/// </summary>
public static class SkipRangeFile
{
    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string PathFor(string videoPath) => videoPath + ".skips.json";

    /// <summary>
    /// Reads the skip ranges saved next to a video. No file means no ranges. Throws
    /// InvalidDataException if the file exists but cannot be read, so the user is told
    /// instead of the ranges quietly disappearing.
    /// </summary>
    public static List<SkipRange> Load(string videoPath, double durationSeconds)
    {
        string path = PathFor(videoPath);
        if (!File.Exists(path))
            return new List<SkipRange>();

        try
        {
            var file = JsonSerializer.Deserialize<FileContent>(File.ReadAllText(path), JsonSettings);
            var ranges = (file?.Skips ?? new List<Entry>()).Select(e => new SkipRange(e.Start, e.End));
            return SkipRanges.Normalize(ranges, durationSeconds);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a valid skip file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves the ranges next to the video. The file is written beside the old one and then
    /// swapped in, so a crash cannot leave half a file. With no ranges the file is deleted.
    /// </summary>
    public static void Save(string videoPath, IReadOnlyList<SkipRange> ranges)
    {
        string path = PathFor(videoPath);

        if (ranges.Count == 0)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        var content = new FileContent
        {
            Video = Path.GetFileName(videoPath),
            Skips = ranges.Select(r => new Entry { Start = Math.Round(r.Start, 3), End = Math.Round(r.End, 3) }).ToList()
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
    }

    private sealed class Entry
    {
        public double Start { get; set; }
        public double End { get; set; }
    }
}
