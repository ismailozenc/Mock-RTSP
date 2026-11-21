namespace MockRtsp;

/// <summary>
/// One video in use by one or more cameras: its details, its own stream settings (if any), and what
/// Mock RTSP Scanner found and labelled in it: the gaps (skips), the motion data, the ground-truth
/// plates and the vehicle labels. Those come from the file next to the video (see VideoSidecar)
/// and are only shown here; when the scanner saves them, <see cref="Poll"/> notices the file's new
/// time and reads it again, so cameras and timelines follow straight away. The only thing this app
/// writes is the video's own stream settings. Used from the UI thread.
/// </summary>
public sealed class VideoFile : IDisposable
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    private readonly string _ffmpegPath;
    private VideoSettings? _ownSettings;
    private List<SkipRange> _skips = new();
    private List<PlateMark> _plates = new();
    private List<VehicleMark> _vehicles = new();
    private List<double> _emptyParts = new();
    private DateTime? _fileTime;            // last-write time of the file read last (null: no file)
    private DateTime _checkedAt = DateTime.MinValue;
    private string? _loadError;             // why the file could not be read, until it can be

    /// <summary>Opens a video: reads its details with FFmpeg. Throws InvalidOperationException if it is not a playable video.</summary>
    public VideoFile(string ffmpegPath, string path)
    {
        _ffmpegPath = ffmpegPath;
        Path = System.IO.Path.GetFullPath(path);
        Info = VideoInfo.Probe(ffmpegPath, Path);
    }

    public string Path { get; }

    public string Name => System.IO.Path.GetFileName(Path);

    public VideoInfo Info { get; }

    public double Duration => Info.DurationSeconds;

    /// <summary>The video as an item for a camera's playlist.</summary>
    public PlaylistItem Item => new(Path, Info);

    /// <summary>The gaps the camera jumps over, as the scanner saved them.</summary>
    public IReadOnlyList<SkipRange> Skips => _skips;

    /// <summary>Ground truth: the licence plates visible in the video, and when.</summary>
    public IReadOnlyList<PlateMark> Plates => _plates;

    /// <summary>Ground truth for vehicle detection: the vehicles in the playing parts, coming or going, with plates.</summary>
    public IReadOnlyList<VehicleMark> Vehicles => _vehicles;

    /// <summary>Moments in playing parts that were checked and have no vehicles, so those parts count as labelled.</summary>
    public IReadOnlyList<double> EmptyParts => _emptyParts;

    /// <summary>How many playing parts there are, how many are labelled, and the vehicles in them.</summary>
    public LabelSummary LabelSummary => VehicleLabels.Summarize(_skips, _vehicles, _emptyParts, Duration);

    /// <summary>Movement over time, from the scanner's motion scan. Null until scanned.</summary>
    public MotionProfile? Motion { get; private set; }

    /// <summary>The settings the gaps were picked with (for the threshold line on the timeline). Null until scanned.</summary>
    public MotionSettings? MotionSettings { get; private set; }

    /// <summary>Preview frames for the timeline, made in the background. Null until Load.</summary>
    public ThumbnailStrip? Previews { get; private set; }

    public bool HasOwnSettings => _ownSettings is not null;

    /// <summary>A copy of the video's own settings, or null when it uses the defaults.</summary>
    public VideoSettings? OwnSettings => _ownSettings?.Clone();

    /// <summary>Raised after something read from the file changed (the scanner saved it), or the settings changed.</summary>
    public event EventHandler? Changed;

    /// <summary>A line for the log.</summary>
    public event EventHandler<string>? Message;

    /// <summary>The settings in force: the video's own, or the defaults.</summary>
    public VideoSettings Effective(StreamOptions defaults) => _ownSettings?.Clone() ?? VideoSettingsOptions.From(defaults);

    // ===================================================================== load and reload

    /// <summary>Reads what is saved next to the video and starts making preview frames.</summary>
    public void Load()
    {
        Read(first: true);
        Previews ??= ThumbnailStrip.Start(_ffmpegPath, Path, Info);
    }

    /// <summary>
    /// Called regularly (UI thread): about once a second, reads the file again when it changed on
    /// disk (saved by the scanner). Returns true when what is shown changed.
    /// </summary>
    public bool Poll()
    {
        if (DateTime.UtcNow - _checkedAt < CheckEvery)
            return false;
        _checkedAt = DateTime.UtcNow;

        if (FileTime() == _fileTime)
            return false;

        bool changed = Read(first: false);
        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
        return changed;
    }

    private DateTime? FileTime()
    {
        string path = VideoSidecar.PathFor(Path);
        string old = VideoSidecar.OldPathFor(Path);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : File.Exists(old) ? File.GetLastWriteTimeUtc(old) : null;
    }

    /// <summary>Reads the file. Returns true when it was read (the caller shows it again).</summary>
    private bool Read(bool first)
    {
        var time = FileTime();
        SavedVideo saved;
        try
        {
            saved = VideoSidecar.Load(Path, Duration);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Being replaced by the scanner right now: try again at the next check.
            if (first)
                Say($"{Name}: the file next to it could not be read yet ({ex.Message}); trying again.");
            _checkedAt = DateTime.UtcNow;
            return false;
        }
        catch (InvalidDataException ex)
        {
            _fileTime = time;
            if (_loadError != ex.Message)
            {
                ErrorReporter.Report(ex, $"Loading saved data for {Path}");
                Say($"ERROR: {Name}: the file saved next to it cannot be read ({ex.Message}). "
                    + (first ? "It plays without gaps until the file is fixed (Mock RTSP Scanner shows it too)." : "What was read before stays in use."));
                _loadError = ex.Message;
            }

            return false;
        }

        _fileTime = time;
        _loadError = null;
        bool skipsChanged = !SameRanges(_skips, saved.Skips);
        _skips = saved.Skips;
        Motion = saved.Motion;
        MotionSettings = saved.MotionSettings;
        _ownSettings = saved.Settings;
        _plates = saved.Plates;
        _vehicles = saved.Vehicles;
        _emptyParts = saved.EmptyParts;

        if (first && saved.FileExists)
        {
            var parts = new List<string> { saved.Scanned ? $"{_skips.Count} gap(s)" : "not scanned for gaps" };
            if (saved.Settings is not null) parts.Add("its own settings");
            if (saved.Plates.Count > 0) parts.Add($"{saved.Plates.Count} plate(s)");
            if (saved.Vehicles.Count > 0 || saved.EmptyParts.Count > 0) parts.Add(LabelSummary.Describe());
            Say($"{Name}: loaded {string.Join(", ", parts)}.");
        }
        else if (first)
        {
            Say($"{Name}: not scanned yet, so it plays in full. Mock RTSP Scanner finds its gaps, plates and directions.");
        }
        else
        {
            Say($"{Name}: updated by the scanner: "
                + (Motion is null ? "not scanned for gaps" : $"{_skips.Count} gap(s){(skipsChanged ? " (changed)" : "")}")
                + (_vehicles.Count > 0 || _emptyParts.Count > 0 ? $", {LabelSummary.Describe()}" : "") + ".");
        }

        return true;
    }

    // ===================================================================== stream settings (the only thing written here)

    /// <summary>Gives the video its own stream settings, or null to go back to the defaults, and saves.</summary>
    public void SetOwnSettings(VideoSettings? settings)
    {
        _ownSettings = settings?.Clone();

        try
        {
            VideoSidecar.SaveStreamSettings(Path, _ownSettings);
            _fileTime = FileTime();         // our own save: nothing to read again
            Say(settings is null ? $"{Name}: uses the default settings again." : $"{Name}: its own settings saved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorReporter.Report(ex, $"Saving settings for {Path}");
            Say($"ERROR: {Name}: the settings are in use but could not be saved next to the video ({ex.Message}). "
                + "They will be lost when the app closes.");
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Say(string message) => Message?.Invoke(this, message);

    private static bool SameRanges(IReadOnlyList<SkipRange> a, IReadOnlyList<SkipRange> b) =>
        a.Count == b.Count
        && a.Zip(b, (x, y) => Math.Abs(x.Start - y.Start) < 0.002 && Math.Abs(x.End - y.End) < 0.002).All(same => same);

    public void Dispose() => Previews?.Dispose();
}
