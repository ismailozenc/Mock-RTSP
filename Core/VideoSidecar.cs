using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockRtsp;

/// <summary>What is saved next to a video.</summary>
public sealed record SavedVideo(
    List<SkipRange> Skips,
    MotionProfile? Motion,
    MotionSettings? MotionSettings,
    VideoSettings? Settings,
    bool FileExists,
    bool FromOldFile,
    List<PlateMark> Plates,
    List<VehicleMark> Vehicles,
    List<double> EmptyParts,
    ReadArea ReadArea)
{
    /// <summary>True once the video has been scanned for motion.</summary>
    public bool Scanned => Motion is not null;

    public static SavedVideo None => new(new List<SkipRange>(), null, null, null, false, false, new List<PlateMark>(),
        new List<VehicleMark>(), new List<double>(), ReadArea.WholePicture);
}

/// <summary>What the scanner keeps about a video (everything in the file except the stream settings).</summary>
public sealed record ScanData(
    IReadOnlyList<SkipRange> Skips,
    MotionProfile? Motion,
    MotionSettings? MotionSettings,
    IReadOnlyList<PlateMark> Plates,
    IReadOnlyList<VehicleMark> Vehicles,
    IReadOnlyList<double> EmptyParts,
    ReadArea ReadArea);

/// <summary>
/// Everything Mock RTSP keeps about one video, in a file next to it:
/// "&lt;video file&gt;.mockrtsp.json" (for example "parking.mp4.mockrtsp.json").
///   settings        the video's own stream settings, if it has any         (written by the RTSP app)
///   skips           the gaps: parts the camera jumps over                  (written by the scanner)
///   motion          how much moves, 5 times a second, from the motion scan  (scanner)
///   motionSettings  the settings the gaps were picked with                  (scanner)
///   plates          ground truth: the licence plates visible, and when      (scanner)
///   vehicles        ground truth: the vehicles in the playing parts         (scanner)
///   emptyParts      moments in playing parts checked as having no vehicles  (scanner)
///   detectionArea, roadPerspective  where plates are read (ReadArea)        (scanner)
/// Both apps may run at once, so each writes only its own keys: it reads the file, replaces its
/// keys and writes it back, all under a lock shared by the two processes (<see cref="Update"/>).
/// The RTSP app notices the scanner's changes by the file's time and reads it again.
/// Copy the file along with the video and all of this comes with it. Files from older versions
/// ("&lt;video file&gt;.skips.json", motion settings inside "settings") are read too and
/// converted on the first save.
/// </summary>
public static class VideoSidecar
{
    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    public static string PathFor(string videoPath) => videoPath + ".mockrtsp.json";

    public static string OldPathFor(string videoPath) => videoPath + ".skips.json";

    /// <summary>
    /// Reads what is saved next to a video. No file means nothing saved. Throws
    /// InvalidDataException if the file exists but cannot be read, so the user is told instead
    /// of the data quietly disappearing.
    /// </summary>
    public static SavedVideo Load(string videoPath, double durationSeconds)
    {
        var (file, fromOld) = Read(videoPath);
        if (file is null)
            return SavedVideo.None;

        try
        {
            return ToSaved(file, fromOld, durationSeconds);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"'{Path.GetFileName(fromOld ? OldPathFor(videoPath) : PathFor(videoPath))}' could not be read: {ex.Message}", ex);
        }
    }

    /// <summary>The RTSP app's part: replaces the video's own stream settings (null: the defaults). Throws IOException, InvalidDataException.</summary>
    public static void SaveStreamSettings(string videoPath, VideoSettings? settings) =>
        Update(videoPath, file => file.Settings = settings is null ? null : SettingsEntry.From(settings));

    /// <summary>The scanner's part: replaces everything but the stream settings. Throws IOException, InvalidDataException.</summary>
    public static void SaveScan(string videoPath, ScanData data) =>
        Update(videoPath, file =>
        {
            file.Skips = data.Skips.Select(r => new Entry { Start = Math.Round(r.Start, 3), End = Math.Round(r.End, 3) }).ToList();
            file.Motion = data.Motion is null ? null : new MotionEntry
            {
                IntervalSeconds = data.Motion.IntervalSeconds,
                Levels = Convert.ToBase64String(data.Motion.Levels)
            };
            file.MotionSettings = data.MotionSettings is null ? null : MotionSettingsEntry.From(data.MotionSettings);
            file.Plates = data.Plates.Count == 0 ? null : data.Plates.Select(p => new PlateEntry
            {
                Time = Math.Round(p.Time, 3),
                Plate = p.Plate,
                X = p.X,
                Y = p.Y,
                Width = p.Width,
                Height = p.Height
            }).ToList();
            file.Vehicles = data.Vehicles.Count == 0 ? null : data.Vehicles.Select(v => new VehicleEntry
            {
                Time = Math.Round(v.Time, 3),
                Direction = v.DirectionText,
                Plate = v.Plate.Length == 0 ? null : v.Plate
            }).ToList();
            file.EmptyParts = data.EmptyParts.Count == 0 ? null : data.EmptyParts.Select(t => Math.Round(t, 3)).ToList();
            file.DetectionArea = data.ReadArea.DetectionText;
            file.RoadPerspective = data.ReadArea.PerspectiveText;
        });

    /// <summary>
    /// Reads the file, lets <paramref name="change"/> replace the caller's keys, and writes it back
    /// (beside, then swapped in, so a crash cannot leave half a file), under a lock both apps share.
    /// With nothing left to keep, the file is deleted. A damaged file is not overwritten
    /// (InvalidDataException): it is left for the user to look at. A file from the previous
    /// version is converted and removed.
    /// </summary>
    private static void Update(string videoPath, Action<FileContent> change)
    {
        using var mutex = new Mutex(false, LockName(videoPath));
        try
        {
            if (!mutex.WaitOne(LockTimeout))
                throw new IOException($"'{Path.GetFileName(PathFor(videoPath))}' is busy (the other Mock RTSP app is saving it). Try again.");
        }
        catch (AbandonedMutexException)
        {
            // The other app ended while saving: the file is whole (atomic writes), carry on.
        }

        try
        {
            var (file, _) = Read(videoPath);
            file ??= new FileContent();
            file.Video = Path.GetFileName(videoPath);
            var legacy = file.Settings?.LegacyMotion();
            change(file);

            // Motion settings from older versions lived inside "settings": keep them where they belong now.
            if (file.MotionSettings is null && legacy is not null && file.Motion is not null)
                file.MotionSettings = MotionSettingsEntry.From(legacy);
            if (file.Settings is not null)
                file.Settings.ClearLegacyMotion();

            string path = PathFor(videoPath);
            if (file.IsEmpty)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            else
            {
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(file, JsonSettings));
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }

            string old = OldPathFor(videoPath);
            if (File.Exists(old))
                File.Delete(old);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    /// <summary>One lock per video file, the same in both apps (named after the full path).</summary>
    private static string LockName(string videoPath)
    {
        string key = Path.GetFullPath(videoPath).ToUpperInvariant();
        return "MockRtsp.Sidecar." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32];
    }

    /// <summary>The file's content (or the previous version's), or null when there is none. Throws InvalidDataException.</summary>
    private static (FileContent? File, bool FromOld) Read(string videoPath)
    {
        string path = PathFor(videoPath);
        bool fromOld = false;
        if (!File.Exists(path))
        {
            path = OldPathFor(videoPath);
            fromOld = true;
            if (!File.Exists(path))
                return (null, false);
        }

        try
        {
            return (JsonSerializer.Deserialize<FileContent>(File.ReadAllText(path), JsonSettings) ?? new FileContent(), fromOld);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' could not be read: {ex.Message}", ex);
        }
    }

    private static SavedVideo ToSaved(FileContent file, bool fromOld, double durationSeconds)
    {
        var ranges = (file.Skips ?? new List<Entry>()).Select(e => new SkipRange(e.Start, e.End));

        MotionProfile? motion = null;
        if (file.Motion is { Levels.Length: > 0, IntervalSeconds: > 0 } m)
            motion = new MotionProfile(m.IntervalSeconds, Convert.FromBase64String(m.Levels));

        MotionSettings? motionSettings = file.MotionSettings?.ToSettings()
                                         ?? file.Settings?.LegacyMotion()
                                         ?? (motion is not null ? MotionSettings.Legacy : null);
        if (motionSettings is not null && motionSettings.Validate() is { Count: > 0 } problems)
            throw new FormatException("motionSettings: " + string.Join(" ", problems));

        var plates = (file.Plates ?? new List<PlateEntry>())
            .Select(p => new PlateMark(p.Time, p.Plate ?? string.Empty, p.X, p.Y, p.Width, p.Height));

        var vehicles = new List<VehicleMark>();
        foreach (var v in file.Vehicles ?? new List<VehicleEntry>())
        {
            if (!VehicleLabels.TryParseDirection(v.Direction, out var direction))
                throw new FormatException($"'{v.Direction}' is not a direction (coming or going)");
            vehicles.Add(new VehicleMark(v.Time, direction, v.Plate ?? string.Empty));
        }

        return new SavedVideo(SkipRanges.Normalize(ranges, durationSeconds), motion, motionSettings, file.Settings?.ToSettings(),
            true, fromOld,
            PlateMarks.Normalize(plates, durationSeconds),
            VehicleLabels.Normalize(vehicles, durationSeconds),
            VehicleLabels.NormalizeEmpty(file.EmptyParts ?? new List<double>(), durationSeconds),
            ReadArea.Parse(file.DetectionArea, file.RoadPerspective));
    }

    private sealed class FileContent
    {
        public string Video { get; set; } = string.Empty;
        public SettingsEntry? Settings { get; set; }
        public List<Entry>? Skips { get; set; } = new();
        public MotionEntry? Motion { get; set; }
        public MotionSettingsEntry? MotionSettings { get; set; }
        public List<PlateEntry>? Plates { get; set; }
        public List<VehicleEntry>? Vehicles { get; set; }
        public List<double>? EmptyParts { get; set; }
        public string? DetectionArea { get; set; }
        public string? RoadPerspective { get; set; }

        [JsonIgnore]
        public bool IsEmpty =>
            Settings is null && (Skips is null || Skips.Count == 0) && Motion is null && MotionSettings is null && Plates is null && Vehicles is null
            && EmptyParts is null && DetectionArea is null && RoadPerspective is null;
    }

    /// <summary>The stream settings, and (read only) the motion settings older versions kept here.</summary>
    private sealed class SettingsEntry
    {
        public bool Loop { get; set; } = true;
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameRate { get; set; }
        public int BitrateKbps { get; set; }
        public int KeyframeInterval { get; set; }
        public bool NoBFrames { get; set; }
        public double? MotionThresholdPercent { get; set; }
        public double? MinStillSeconds { get; set; }
        public double? MotionPaddingSeconds { get; set; }

        public static SettingsEntry From(VideoSettings s) => new()
        {
            Loop = s.Loop,
            Width = s.Width,
            Height = s.Height,
            FrameRate = s.FrameRate,
            BitrateKbps = s.BitrateKbps,
            KeyframeInterval = s.KeyframeInterval,
            NoBFrames = s.NoBFrames
        };

        public VideoSettings ToSettings() => new()
        {
            Loop = Loop,
            Width = Width,
            Height = Height,
            FrameRate = FrameRate,
            BitrateKbps = BitrateKbps,
            KeyframeInterval = KeyframeInterval,
            NoBFrames = NoBFrames
        };

        public MotionSettings? LegacyMotion() =>
            MotionThresholdPercent is double t && MinStillSeconds is double s && MotionPaddingSeconds is double p
                ? new MotionSettings(t, s, p)
                : null;

        public void ClearLegacyMotion() => MotionThresholdPercent = MinStillSeconds = MotionPaddingSeconds = null;
    }

    private sealed class MotionSettingsEntry
    {
        public double ThresholdPercent { get; set; }
        public double MinStillSeconds { get; set; }
        public double PaddingSeconds { get; set; }

        public static MotionSettingsEntry From(MotionSettings s) =>
            new() { ThresholdPercent = s.ThresholdPercent, MinStillSeconds = s.MinStillSeconds, PaddingSeconds = s.PaddingSeconds };

        public MotionSettings ToSettings() => new(ThresholdPercent, MinStillSeconds, PaddingSeconds);
    }

    /// <summary>A ground-truth vehicle: when, "coming" or "going", and its plate (left out when not given).</summary>
    private sealed class VehicleEntry
    {
        public double Time { get; set; }
        public string? Direction { get; set; }
        public string? Plate { get; set; }
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
