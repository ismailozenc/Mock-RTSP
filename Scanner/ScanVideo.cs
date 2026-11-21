namespace MockRtsp;

/// <summary>What changed in a <see cref="ScanVideo"/>.</summary>
[Flags]
public enum VideoChange
{
    None = 0,
    Gaps = 1,           // skips, so the playing parts
    Motion = 2,         // motion data (also while a motion scan is running) or its settings
    Labels = 4,         // vehicles, parts checked empty, parts to check
    Plates = 8,         // ground-truth plates
    Area = 16,          // detection area, road perspective
    Reading = 32,       // the plates read (a part was read, a new reading started)
    All = 63
}

/// <summary>
/// One video open in the scanner, and everything the scanner keeps about it: the gaps (skips) with
/// the motion data and the settings that picked them, the ground-truth plates, the vehicle labels,
/// where plates are read (all in "&lt;video&gt;.mockrtsp.json", see VideoSidecar), and the plate
/// reading ("&lt;video&gt;.mockrtsp.plates.json", see PlateScanFile). Every change is saved at once,
/// so the RTSP app shows it straight away. While a scan runs its results arrive here part by part
/// (<see cref="AddReadPart"/>) and the user can keep editing. Used from the UI thread only.
/// </summary>
internal sealed class ScanVideo : IDisposable
{
    private List<SkipRange> _skips = new();
    private List<PlateMark> _plates = new();
    private List<VehicleMark> _vehicles = new();
    private List<double> _emptyParts = new();
    private readonly List<double> _toCheck = new();     // moments in parts prefilled and not looked at yet
    private MotionProfile? _liveMotion;                 // measured so far by a running motion scan

    private ScanVideo(string path, VideoInfo info)
    {
        Path = path;
        Info = info;
    }

    public string Path { get; }

    public string Name => System.IO.Path.GetFileName(Path);

    public VideoInfo Info { get; }

    public double Duration => Info.DurationSeconds;

    /// <summary>Why the files next to the video cannot be read; nothing is saved (or scanned) until they can.</summary>
    public string? LoadError { get; private set; }

    public bool CanEdit => LoadError is null;

    public IReadOnlyList<SkipRange> Skips => _skips;

    /// <summary>The motion data: the saved one, or what a running scan measured so far.</summary>
    public MotionProfile? Motion => _liveMotion ?? SavedMotion;

    public MotionProfile? SavedMotion { get; private set; }

    /// <summary>The settings the gaps were picked with; null when never scanned.</summary>
    public MotionSettings? MotionSettings { get; private set; }

    public IReadOnlyList<PlateMark> Plates => _plates;

    public IReadOnlyList<VehicleMark> Vehicles => _vehicles;

    public IReadOnlyList<double> EmptyParts => _emptyParts;

    public ReadArea ReadArea { get; private set; } = ReadArea.WholePicture;

    /// <summary>The plates read (complete, or growing while a reading runs); null when never read.</summary>
    public PlateScan? Reading { get; private set; }

    /// <summary>Preview frames for the timeline's hover picture.</summary>
    public ThumbnailStrip? Previews { get; private set; }

    public List<PlayingPart> Parts => VehicleLabels.PlayingParts(_skips, Duration);

    public LabelSummary Summary => VehicleLabels.Summarize(_skips, _vehicles, _emptyParts, Duration);

    public bool Scanned => SavedMotion is not null;

    /// <summary>True when the gaps are exactly what the motion data gives with its settings (none changed by hand).</summary>
    public bool GapsFromMotion =>
        SavedMotion is null ? _skips.Count == 0
        : MotionSettings is not null && SameRanges(_skips, MotionSettings.StillParts(SavedMotion, Duration));

    public event EventHandler<VideoChange>? Changed;

    /// <summary>A line for the log.</summary>
    public event EventHandler<string>? Message;

    // ===================================================================== open

    /// <summary>Opens a video (FFmpeg reads its details) and reads its files. Throws InvalidOperationException if it is not a playable video.</summary>
    public static ScanVideo Open(string ffmpegPath, string path)
    {
        string full = System.IO.Path.GetFullPath(path);
        var video = new ScanVideo(full, VideoInfo.Probe(ffmpegPath, full));
        video.Load();
        video.Previews = ThumbnailStrip.Start(ffmpegPath, full, video.Info);
        return video;
    }

    private void Load()
    {
        try
        {
            var saved = VideoSidecar.Load(Path, Duration);
            _skips = saved.Skips;
            SavedMotion = saved.Motion;
            MotionSettings = saved.MotionSettings;
            _plates = saved.Plates;
            _vehicles = saved.Vehicles;
            _emptyParts = saved.EmptyParts;
            ReadArea = saved.ReadArea;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LoadError = ex.Message;
            return;
        }

        try
        {
            Reading = PlateScanFile.Load(Path);
            _toCheck.AddRange(Reading?.ToCheck ?? Array.Empty<double>());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Say($"{Name}: the plates read earlier cannot be shown ({ex.Message}). Reading them again replaces the file.");
        }
    }

    // ===================================================================== gaps

    /// <summary>Replaces the gaps (edited on the timeline) and saves them.</summary>
    public void SetSkips(IEnumerable<SkipRange> ranges)
    {
        _skips = SkipRanges.Normalize(ranges, Duration);
        if (!SkipRanges.LeavesSomethingToPlay(_skips, Duration))
            Say($"{Name}: every part is marked as a gap, so the RTSP app ignores the gaps until some are removed.");
        Save(VideoChange.Gaps);
    }

    /// <summary>What a running motion scan measured so far (shown, not saved).</summary>
    public void ShowLiveMotion(MotionProfile? soFar)
    {
        _liveMotion = soFar;
        Changed?.Invoke(this, VideoChange.Motion);
    }

    /// <summary>
    /// A finished motion scan: keeps the data and picks the gaps with <paramref name="settings"/>.
    /// Gaps changed by hand are kept (the user can pick them again from the motion data).
    /// </summary>
    public void SetMotion(MotionProfile motion, MotionSettings settings)
    {
        bool replace = GapsFromMotion;
        _liveMotion = null;
        SavedMotion = motion;

        if (replace)
        {
            MotionSettings = settings;
            _skips = settings.StillParts(motion, Duration);
            double cut = _skips.Sum(r => r.Length);
            Say(_skips.Count == 0
                ? $"{Name}: gaps found: none (movement throughout, or none over {settings.ThresholdPercent:0.0} %), so it plays in full."
                : $"{Name}: gaps found: {_skips.Count}, {Time(cut)} of {Time(Duration)} cut; {Parts.Count} playing part(s).");
        }
        else
        {
            MotionSettings ??= settings;
            Say($"{Name}: motion measured again; the gaps you changed by hand are kept (Pick gaps again replaces them).");
        }

        Save(VideoChange.Gaps | VideoChange.Motion);
    }

    /// <summary>Stops showing a cancelled motion scan's partial data.</summary>
    public void ClearLiveMotion()
    {
        if (_liveMotion is null)
            return;
        _liveMotion = null;
        Changed?.Invoke(this, VideoChange.Motion);
    }

    /// <summary>Picks the gaps again from the saved motion data with these settings. Instant: no rescan.</summary>
    public void PickGaps(MotionSettings settings)
    {
        if (SavedMotion is null)
            throw new InvalidOperationException($"{Name} has no motion data yet: scan it first.");

        MotionSettings = settings;
        _skips = settings.StillParts(SavedMotion, Duration);
        Say($"{Name}: gaps picked again ({settings.Describe()}): {_skips.Count} gap(s), {Parts.Count} playing part(s).");
        Save(VideoChange.Gaps | VideoChange.Motion);
    }

    // ===================================================================== labels

    /// <summary>Replaces the vehicles of one playing part (and whether it is checked as having none), and saves.</summary>
    public void SetPartLabels(PlayingPart part, IReadOnlyList<VehicleMark> vehicles, bool markedEmpty)
    {
        var keep = _vehicles.Where(v => !part.Contains(v.Time, Duration)).Concat(vehicles).ToList();
        var empty = _emptyParts.Where(t => !part.Contains(t, Duration)).ToList();
        if (vehicles.Count == 0 && markedEmpty)
            empty.Add(Math.Round(part.Middle, 3));

        var newVehicles = VehicleLabels.Normalize(keep, Duration);
        var newEmpty = VehicleLabels.NormalizeEmpty(empty, Duration);
        if (newVehicles.SequenceEqual(_vehicles) && newEmpty.SequenceEqual(_emptyParts))
            return;

        _vehicles = newVehicles;
        _emptyParts = newEmpty;
        Save(VideoChange.Labels);
    }

    /// <summary>Deletes the labels that lie in gaps now (never streamed).</summary>
    public int DeleteLabelsInGaps()
    {
        int count = VehicleLabels.CountOutside(Parts, _vehicles, _emptyParts, Duration);
        if (count == 0)
            return 0;

        var parts = Parts;
        _vehicles = _vehicles.Where(v => parts.Any(p => p.Contains(v.Time, Duration))).ToList();
        _emptyParts = _emptyParts.Where(t => parts.Any(p => p.Contains(t, Duration))).ToList();
        Save(VideoChange.Labels);
        return count;
    }

    public void SetPlates(IEnumerable<PlateMark> plates)
    {
        _plates = PlateMarks.Normalize(plates, Duration);
        Save(VideoChange.Plates);
    }

    public void SetReadArea(ReadArea area)
    {
        if (area.SameAs(ReadArea.DetectionText, ReadArea.PerspectiveText))
            return;
        ReadArea = area;
        Save(VideoChange.Area);
    }

    // ===================================================================== parts to check

    /// <summary>True when the part was prefilled from the plates and not looked at yet.</summary>
    public bool IsToCheck(PlayingPart part) =>
        _toCheck.Any(t => part.Contains(t, Duration)) && _vehicles.Any(v => part.Contains(v.Time, Duration));

    public int ToCheckCount => Parts.Count(IsToCheck);

    /// <summary>The part has been looked at: its prefilled labels count as checked.</summary>
    public void MarkChecked(PlayingPart part)
    {
        if (_toCheck.RemoveAll(t => part.Contains(t, Duration)) > 0)
            SaveReading(VideoChange.Labels);
    }

    // ===================================================================== reading plates

    /// <summary>A new reading starts: the old one is replaced as soon as the first part is read.</summary>
    public void BeginReading(PlateScan empty)
    {
        Reading = empty with { ToCheck = null };
        SaveReading(VideoChange.Reading);
    }

    /// <summary>
    /// One part read: its cars are added to the reading and, when the part has no labels yet, it
    /// is prefilled with them (see <see cref="Prefill"/>). Returns the prefill's note, or null.
    /// </summary>
    public string? AddReadPart(ScannedPart part, IReadOnlyList<PlateCar> cars)
    {
        if (Reading is not { } reading)
            return null;

        Reading = reading with
        {
            Parts = reading.Parts.Append(part).ToList(),
            Cars = reading.Cars.Concat(cars).OrderBy(c => c.Time).ToList()
        };

        var playing = Parts.FirstOrDefault(p => p.Contains((part.Start + part.End) / 2, Duration));
        string? note = playing is null ? null : PrefillPart(playing, out _);
        SaveReading(VideoChange.Reading | VideoChange.Labels);
        if (note is null)
            return null;

        Save(VideoChange.Labels);
        return note;
    }

    /// <summary>Prefills every unlabelled part from the plates read (for a reading made earlier).</summary>
    public string Prefill()
    {
        if (Reading is null)
            return Loc.T("No plates read yet.");

        int parts = 0, vehicles = 0, unknown = 0;
        foreach (var part in Parts)
        {
            string? note = PrefillPart(part, out int added);
            if (note is null)
                continue;
            if (added > 0)
            {
                parts++;
                vehicles += added;
            }
            else
            {
                unknown++;
            }
        }

        if (parts > 0)
        {
            Save(VideoChange.Labels);
            SaveReading(VideoChange.Labels);
        }

        string done = parts == 0
            ? Loc.T("No part without labels could be prefilled.")
            : Loc.T("Prefilled {0} part(s) with {1} vehicle(s) (most likely plate and direction): check them with Next to label.", parts, vehicles);
        return unknown == 0 ? done : done + " " + Loc.T("{0} part(s) left to label: nothing tells a car's direction there.", unknown);
    }

    /// <summary>
    /// Fills one part that has no labels yet (no vehicles, not checked as empty) with the cars read in
    /// it: each at its best read's time, with its most likely direction and its plate (voted, or the
    /// best guess). Cars seen in fewer than [Travel] MinReads pictures are left out as glimpses; a part
    /// with a car whose direction nothing tells is left to label. A prefilled part is marked to check.
    /// Returns a note when the part had cars to prefill (added = 0: left to label), else null.
    /// </summary>
    private string? PrefillPart(PlayingPart part, out int added)
    {
        added = 0;
        if (Reading is null || _vehicles.Any(v => part.Contains(v.Time, Duration)) || _emptyParts.Any(t => part.Contains(t, Duration)))
            return null;

        int minReads = PlateScanSettings.BirelPlakaFactory.Travel.MinReads;
        var cars = Reading.CarsIn(part, Duration).Where(c => c.Reads >= minReads).ToList();
        if (cars.Count == 0)
            return null;
        if (cars.Any(c => c.MostLikelyDirection is null))
            return Loc.T("part {0}: nothing tells a car's direction, left to label", part.Number);

        foreach (var car in cars)
            _vehicles.Add(new VehicleMark(Math.Round(car.Time, 3), car.MostLikelyDirection!.Value, car.Plate));
        _vehicles = VehicleLabels.Normalize(_vehicles, Duration);
        _toCheck.Add(Math.Round(part.Middle, 3));
        added = cars.Count;
        return Loc.T("part {0} prefilled with {1} vehicle(s)", part.Number, cars.Count);
    }

    /// <summary>A reading that ended (finished or stopped): saved as it is.</summary>
    public void EndReading() => SaveReading(VideoChange.Reading);

    // ===================================================================== saving

    private void Save(VideoChange change)
    {
        if (LoadError is not null)
            throw new InvalidOperationException($"{Name}: its file cannot be read ({LoadError}), so nothing is saved. Fix or delete the file first.");

        try
        {
            VideoSidecar.SaveScan(Path, new ScanData(_skips, SavedMotion, MotionSettings, _plates, _vehicles, _emptyParts, ReadArea));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorReporter.Report(ex, $"Saving {Path}");
            Say($"ERROR: {Name}: the change is shown but could not be saved next to the video ({ex.Message}).");
        }

        Changed?.Invoke(this, change);
    }

    private void SaveReading(VideoChange change)
    {
        if (Reading is { } reading)
        {
            var moments = _toCheck.Distinct().OrderBy(t => t).ToList();
            Reading = reading with { ToCheck = moments.Count == 0 ? null : moments };
            try
            {
                PlateScanFile.Save(Path, Reading);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ErrorReporter.Report(ex, $"Saving the plates read in {Path}");
                Say($"ERROR: {Name}: the plates read could not be saved next to the video ({ex.Message}).");
            }
        }

        Changed?.Invoke(this, change);
    }

    private void Say(string message) => Message?.Invoke(this, message);

    private static bool SameRanges(IReadOnlyList<SkipRange> a, IReadOnlyList<SkipRange> b) =>
        a.Count == b.Count
        && a.Zip(b, (x, y) => Math.Abs(x.Start - y.Start) < 0.002 && Math.Abs(x.End - y.End) < 0.002).All(same => same);

    private static string Time(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, Math.Round(seconds, 1)));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss\.f") : time.ToString(@"mm\:ss\.f");
    }

    public void Dispose() => Previews?.Dispose();
}
