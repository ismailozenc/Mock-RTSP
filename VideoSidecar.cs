using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockRtsp;

/// <summary>What is saved next to a video.</summary>
public sealed record SavedVideo(
    List<SkipRange> Skips,
    MotionProfile? Motion,
    VideoSettings? Settings,
    bool FileExists,
    bool FromOldFile,
    List<PlateMark> Plates)
{
    /// <summary>True once the video has been scanned for motion, so it is not scanned again on load.</summary>
    public bool Scanned => Motion is not null;

    public static SavedVideo None => new(new List<SkipRange>(), null, null, false, false, new List<PlateMark>());
}

/// <summary>
/// Everything Mock RTSP keeps about one video, in a file next to it:
/// "&lt;video file&gt;.mockrtsp.json" (for example "parking.mp4.mockrtsp.json").
///   settings  the video's own settings, if it has any (otherwise it uses the defaults)
///   skips     the parts the camera jumps over
///   motion    how much moves, 5 times a second, from the motion scan; kept so changing the
///             motion settings picks new skips straight away, without scanning again
///   plates    ground truth: the licence plates visible in the video, and when
/// Copy the file along with the video and all of this comes with it.
/// Files from the previous version ("&lt;video file&gt;.skips.json") are read too, and replaced
/// by the new file the first time it is saved.
/// </summary>
public static class VideoSidecar
{
    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string PathFor(string videoPath) => videoPath + ".mockrtsp.json";

    public static string OldPathFor(string videoPath) => videoPath + ".skips.json";

    /// <summary>
    /// Reads what is saved next to a video. No file means nothing saved. Throws
    /// InvalidDataException if the file exists but cannot be read, so the user is told instead
    /// of the data quietly disappearing.
    /// </summary>
    public static SavedVideo Load(string videoPath, double durationSeconds)
    {
        string path = PathFor(videoPath);
        bool fromOld = false;

        if (!File.Exists(path))
        {
            path = OldPathFor(videoPath);
            fromOld = true;
            if (!File.Exists(path))
                return SavedVideo.None;
        }

        try
        {
            var file = JsonSerializer.Deserialize<FileContent>(File.ReadAllText(path), JsonSettings);
            var ranges = (file?.Skips ?? new List<Entry>()).Select(e => new SkipRange(e.Start, e.End));

            MotionProfile? motion = null;
            if (file?.Motion is { Levels.Length: > 0, IntervalSeconds: > 0 } m)
                motion = new MotionProfile(m.IntervalSeconds, Convert.FromBase64String(m.Levels));

            var plates = (file?.Plates ?? new List<PlateEntry>())
                .Select(p => new PlateMark(p.Time, p.Plate ?? string.Empty, p.X, p.Y, p.Width, p.Height));

            return new SavedVideo(SkipRanges.Normalize(ranges, durationSeconds), motion, file?.Settings, true, fromOld,
                PlateMarks.Normalize(plates, durationSeconds));
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' could not be read: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves next to the video. The file is written beside the old one and then swapped in, so a
    /// crash cannot leave half a file. With nothing to keep, the file is deleted. A file from
    /// the previous version is removed once its content is in the new file.
    /// </summary>
    public static void Save(string videoPath, IReadOnlyList<SkipRange> ranges, MotionProfile? motion, VideoSettings? settings,
        IReadOnlyList<PlateMark>? plates = null)
    {
        string path = PathFor(videoPath);
        plates ??= Array.Empty<PlateMark>();

        if (ranges.Count == 0 && motion is null && settings is null && plates.Count == 0)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        else
        {
            var content = new FileContent
            {
                Video = Path.GetFileName(videoPath),
                Settings = settings,
                Skips = ranges.Select(r => new Entry { Start = Math.Round(r.Start, 3), End = Math.Round(r.End, 3) }).ToList(),
                Plates = plates.Count == 0 ? null : plates.Select(p => new PlateEntry
                {
                    Time = Math.Round(p.Time, 3),
                    Plate = p.Plate,
                    X = p.X,
                    Y = p.Y,
                    Width = p.Width,
                    Height = p.Height
                }).ToList(),
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

        string old = OldPathFor(videoPath);
        if (File.Exists(old))
            File.Delete(old);
    }

    private sealed class FileContent
    {
        public string Video { get; set; } = string.Empty;
        public VideoSettings? Settings { get; set; }
        public List<Entry> Skips { get; set; } = new();
        public MotionEntry? Motion { get; set; }
        public List<PlateEntry>? Plates { get; set; }
    }

    /// <summary>A ground-truth plate; the region (fractions of the picture) is left out when not given.</summary>
    private sealed class PlateEntry
    {
        public double Time { get; set; }
        public string? Plate { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
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
