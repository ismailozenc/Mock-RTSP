namespace MockRtsp;

/// <summary>
/// One video in use by one or more cameras: its details, its own settings (if any), its skip
/// ranges, its motion scan, and the preview frames for the timeline. Everything that is saved
/// goes to the file next to the video (see VideoSidecar) as soon as it changes.
/// Used from the UI thread; a motion scan runs in the background and is picked up by Poll.
/// </summary>
public sealed class VideoFile : IDisposable
{
    private readonly string _ffmpegPath;
    private VideoSettings? _ownSettings;
    private List<SkipRange> _skips = new();
    private MotionScan? _scan;
    private bool _scanIsAutomatic;

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

    public IReadOnlyList<SkipRange> Skips => _skips;

    /// <summary>Movement over time, from the motion scan. Null until scanned.</summary>
    public MotionProfile? Motion { get; private set; }

    /// <summary>Preview frames for the timeline, made in the background. Null until Load.</summary>
    public ThumbnailStrip? Previews { get; private set; }

    public bool HasOwnSettings => _ownSettings is not null;

    /// <summary>A copy of the video's own settings, or null when it uses the defaults.</summary>
    public VideoSettings? OwnSettings => _ownSettings?.Clone();

    public bool IsScanning => _scan is not null;

    /// <summary>Progress of a running motion scan, 0 to 1, or null.</summary>
    public double? ScanProgress => _scan?.Progress;

    /// <summary>Raised after the skips, the motion data or the settings change.</summary>
    public event EventHandler? Changed;

    /// <summary>A line for the log.</summary>
    public event EventHandler<string>? Message;

    /// <summary>The settings in force: the video's own, or the defaults.</summary>
    public VideoSettings Effective(StreamOptions defaults) => _ownSettings?.Clone() ?? VideoSettings.From(defaults);

    // ===================================================================== load

    /// <summary>
    /// Reads what is saved next to the video and starts making preview frames. A video seen for
    /// the first time is scanned for motion when the defaults say so.
    /// </summary>
    public void Load(StreamOptions defaults)
    {
        try
        {
            var saved = VideoSidecar.Load(Path, Duration);
            _skips = saved.Skips;
            Motion = saved.Motion;
            _ownSettings = saved.Settings;

            if (saved.FileExists)
            {
                var parts = new List<string> { $"{_skips.Count} skip(s)" };
                if (saved.Settings is not null) parts.Add("its own settings");
                if (saved.Scanned) parts.Add("motion data");
                Say($"{Name}: loaded {string.Join(", ", parts)}"
                    + (saved.FromOldFile ? $" from {System.IO.Path.GetFileName(VideoSidecar.OldPathFor(Path))} (moved to {System.IO.Path.GetFileName(VideoSidecar.PathFor(Path))} on the next change)." : "."));
            }
            else if (defaults.AutoScanMotion)
            {
                StartScan(automatic: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorReporter.Report(ex, $"Loading saved data for {Path}");
            Say($"ERROR: {Name}: could not read the file saved next to it ({ex.Message}). It plays without skips; "
                + "the file is left as it is until you change something or scan again.");
        }

        Previews ??= ThumbnailStrip.Start(_ffmpegPath, Path, Info);
    }

    // ===================================================================== skips

    /// <summary>Replaces the skip ranges and saves them.</summary>
    public void SetSkips(IEnumerable<SkipRange> ranges)
    {
        _skips = SkipRanges.Normalize(ranges, Duration);

        if (!SkipRanges.LeavesSomethingToPlay(_skips, Duration))
            Say($"{Name}: every part is marked to skip, so the skips are ignored until some are removed.");

        if (Save())
        {
            Say(_skips.Count == 0
                ? $"{Name}: no skips."
                : $"{Name}: {_skips.Count} skip(s) saved: " + string.Join(", ", _skips.Select(r => $"{Time(r.Start)}-{Time(r.End)}")));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True when the skips are exactly what the motion scan picks under these settings (none edited by hand).</summary>
    public bool SkipsMatchScan(VideoSettings settings) =>
        Motion is not null && SameRanges(_skips, settings.StillParts(Motion, Duration));

    /// <summary>Picks the skips again from the saved motion data, for new motion settings. Instant: no rescan.</summary>
    public void ApplyMotionSettings(VideoSettings settings)
    {
        if (Motion is null)
            return;

        var still = settings.StillParts(Motion, Duration);
        SetSkips(still);
        Say($"{Name}: skips picked again from the saved motion data ({settings.MotionThresholdPercent:0.0} %, "
            + $"still parts over {settings.MinStillSeconds:0.#} s, {settings.MotionPaddingSeconds:0.#} s kept around movement). No rescan needed.");
    }

    // ===================================================================== settings

    /// <summary>Gives the video its own settings, or null to go back to the defaults, and saves.</summary>
    public void SetOwnSettings(VideoSettings? settings)
    {
        _ownSettings = settings?.Clone();

        if (Save())
            Say(settings is null ? $"{Name}: uses the default settings again." : $"{Name}: its own settings saved.");

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ===================================================================== motion scan

    public void StartScan(bool automatic)
    {
        if (_scan is not null)
            return;

        _scan = MotionScan.Start(_ffmpegPath, Path, Info);
        _scanIsAutomatic = automatic;
        Say($"{Name}: scanning for movement"
            + (automatic ? " (first time this video is used). It plays in full until the scan is done." : "."));
    }

    /// <summary>Called regularly: applies a finished motion scan. Returns true if something changed.</summary>
    public bool Poll(StreamOptions defaults)
    {
        if (_scan is not { IsComplete: true } scan)
            return false;

        _scan = null;                                   // finished: its FFmpeg has exited, nothing to dispose

        if (scan.IsCancelled)
            return false;

        if (scan.Result is not { } profile)
        {
            Say($"ERROR: {Name}: the motion scan failed: {scan.Error}");
            return false;
        }

        Motion = profile;
        var settings = Effective(defaults);

        // An automatic scan never replaces skips made while it ran.
        if (_scanIsAutomatic && _skips.Count > 0)
        {
            Save();
            Say($"{Name}: motion scan done. Your skips were kept; Scan motion replaces them with the scan.");
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        var still = settings.StillParts(profile, Duration);
        SetSkips(still);

        double skipped = still.Sum(r => r.Length);
        if (still.Count == 0)
            Say($"{Name}: motion scan found nothing to skip"
                + (profile.Levels.All(l => l < settings.MotionThresholdPercent * 10)
                    ? $" (no movement over {settings.MotionThresholdPercent:0.0} %, so the whole video plays; lower Movement needed in its settings)."
                    : " (movement throughout)."));
        else
            Say($"{Name}: motion scan skips {still.Count} still part(s), {Time(skipped)} of {Time(Duration)}; "
                + $"{Time(Duration - skipped)} with movement plays.");

        return true;
    }

    // ===================================================================== saving

    private bool Save()
    {
        try
        {
            VideoSidecar.Save(Path, _skips, Motion, _ownSettings);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorReporter.Report(ex, $"Saving data for {Path}");
            Say($"ERROR: {Name}: changes are in use but could not be saved next to the video ({ex.Message}). "
                + "They will be lost when the app closes. Check that the video's folder is writable.");
            return false;
        }
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

    public void Dispose()
    {
        _scan?.Dispose();
        _scan = null;
        Previews?.Dispose();
    }
}
