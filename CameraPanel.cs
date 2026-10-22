namespace MockRtsp;

/// <summary>What a camera panel needs from the main window.</summary>
public interface ICameraHost
{
    /// <summary>True while the main window is doing something that must not be interrupted.</summary>
    bool Busy { get; }

    /// <summary>The main settings: defaults for videos without their own.</summary>
    StreamOptions Defaults { get; }

    void Log(string message);

    /// <summary>Runs a UI action, showing any error in a dialog and the log.</summary>
    void Run(Action action, string context);

    Task RunAsync(Func<Task> action, string context);

    void StartCamera(CameraEntry entry);
    void StopCamera(CameraEntry entry);
    void RemoveCamera(CameraEntry entry);
    Task OpenInVlcAsync(CameraEntry entry);
    Task SimulateOutageAsync(CameraEntry entry, int seconds);
    Task EditVideoSettingsAsync(CameraEntry entry, VideoFile video);
    Task EditPlaylistAsync(CameraEntry entry);
    void ScanMotion(VideoFile video);
}

/// <summary>
/// Everything for one camera in one place: its state and actions, a player row (video of the
/// playlist, rewind, pause, back, forward, next, speed, skips, zoom) and its own timeline, which
/// shows the video playing now. No camera needs selecting first.
/// The layout lives in CameraPanel.Designer.cs.
/// </summary>
public sealed partial class CameraPanel : UserControl
{
    private static readonly double[] Speeds = { 0.25, 0.5, 1.0, 2.0, 4.0 };     // matches the _speed items

    private readonly ICameraHost? _host;
    private bool _updating;                 // set while code, not the user, changes the controls
    private VideoFile? _shown;              // the video on the timeline: skip edits go to this one
    private string _playlistShown = string.Empty;

    /// <summary>Parameterless constructor for the designer.</summary>
    public CameraPanel()
    {
        InitializeComponent();
    }

    public CameraPanel(ICameraHost host, CameraEntry entry) : this()
    {
        _host = host;
        Entry = entry;
        _path.Text = entry.StreamPath;
        UpdateView();
    }

    public CameraEntry? Entry { get; }

    /// <summary>The height that shows everything at a width, rows wrapping as needed.</summary>
    public int HeightFor(int width)
    {
        int inner = Math.Max(100, width - Padding.Horizontal - 2);
        return Padding.Vertical + 2
               + _header.GetPreferredSize(new Size(inner, 0)).Height + _header.Margin.Vertical
               + _player.GetPreferredSize(new Size(inner, 0)).Height + _player.Margin.Vertical
               + _timeline.Height + _timeline.Margin.Vertical;
    }

    // ===================================================================== refresh

    /// <summary>Shows the camera's current state. Called by the main window's timer.</summary>
    public void UpdateView()
    {
        if (_host is null || Entry is null)
            return;

        var camera = Entry.Camera;
        var video = Entry.Video;
        var playlist = Entry.Playlist;
        int index = Math.Clamp(camera.ItemIndex, 0, playlist.Count - 1);
        bool online = camera.IsOnline;
        bool idle = !_host.Busy;
        bool several = playlist.Count > 1;

        _updating = true;
        try
        {
            _shown = video;

            _state.Text = !online ? "OFF" : camera.IsPaused ? "PAUSED" : "LIVE";
            _state.BackColor = !online ? Color.DimGray : camera.IsPaused ? Color.DarkOrange : Color.ForestGreen;

            var format = camera.Format;
            var settings = video.Effective(_host.Defaults);
            string skips = video.Skips.Count == 0 ? "no skips" : $"{video.Skips.Count} skip(s)";
            _details.Text = (several ? $"{playlist.Count} videos   " : $"{video.Name}   ")
                            + $"{format.Width}x{format.Height} {format.FrameRate} fps   "
                            + $"{(video.HasOwnSettings ? "own settings" : "default settings")}   {skips}";

            // The playlist picker, rebuilt when the playlist changes.
            string names = string.Join("|", playlist.Select(v => v.Path));
            if (names != _playlistShown)
            {
                _playlistShown = names;
                _video.Items.Clear();
                _video.Items.AddRange(playlist.Select((v, i) => (object)$"{i + 1}/{playlist.Count}  {v.Name}").ToArray());
            }

            if (_video.SelectedIndex != index)
                _video.SelectedIndex = index;

            if (_video.Visible != several)
                _video.Visible = several;
            if (_next.Visible != several)
                _next.Visible = several;

            _startStop.Text = online ? "Stop" : "Start";
            _startStop.Enabled = idle;
            _openVlc.Enabled = idle && online;
            _playlist.Enabled = idle;
            _settings.Enabled = idle;
            _scanMotion.Enabled = idle && !video.IsScanning;
            _scanMotion.Text = video.IsScanning ? $"Scanning {video.ScanProgress * 100:0}%" : "Scan motion";
            _outage.Enabled = idle;
            _remove.Enabled = idle;

            _video.Enabled = idle;
            _rewind.Enabled = idle;
            _back10.Enabled = idle;
            _playPause.Enabled = idle;
            _forward10.Enabled = idle;
            _next.Enabled = idle;
            _speed.Enabled = idle;
            _skipStart.Enabled = idle;
            _skipEnd.Enabled = idle && Entry.PendingSkipStart is not null && ReferenceEquals(Entry.PendingSkipVideo, video);
            _timeline.Enabled = idle;

            _playPause.Text = camera.IsPaused ? "Play" : "Pause";
            _time.Text = $"{FormatTime(camera.Position)} / {FormatTime(camera.Duration)}";

            int speedIndex = Array.IndexOf(Speeds, camera.Speed);
            if (_speed.SelectedIndex != speedIndex)
                _speed.SelectedIndex = speedIndex;

            _timeline.SetSource(video.Previews, video.Duration);
            _timeline.SetSkipRanges(video.Skips);
            _timeline.SetMotion(video.Motion, settings.MotionThresholdPercent);
            _timeline.ScanProgress = video.ScanProgress;
            _timeline.PendingStart = ReferenceEquals(Entry.PendingSkipVideo, video) ? Entry.PendingSkipStart : null;
            _timeline.Position = camera.Position.TotalSeconds;
            _timeline.Invalidate();                 // new preview frames may have arrived
        }
        finally
        {
            _updating = false;
        }
    }

    // ===================================================================== camera actions

    private void OnStartStopClick(object? sender, EventArgs e) => Do(entry =>
    {
        if (entry.Camera.IsOnline)
            _host!.StopCamera(entry);
        else
            _host!.StartCamera(entry);
    }, "Start/Stop");

    private async void OnOpenVlcClick(object? sender, EventArgs e) => await DoAsync(entry => _host!.OpenInVlcAsync(entry), "Open in VLC");

    private async void OnSettingsClick(object? sender, EventArgs e) =>
        await DoAsync(entry => _host!.EditVideoSettingsAsync(entry, _shown ?? entry.Video), "Video settings");

    private async void OnPlaylistClick(object? sender, EventArgs e) => await DoAsync(entry => _host!.EditPlaylistAsync(entry), "Playlist");

    private void OnScanMotionClick(object? sender, EventArgs e) => Do(entry => _host!.ScanMotion(_shown ?? entry.Video), "Scan motion");

    private async void OnOutageClick(object? sender, EventArgs e) =>
        await DoAsync(entry => _host!.SimulateOutageAsync(entry, (int)_outageSeconds.Value), "Simulate outage");

    private void OnRemoveClick(object? sender, EventArgs e) => Do(entry => _host!.RemoveCamera(entry), "Remove camera");

    // ===================================================================== player

    private void OnPlayPauseClick(object? sender, EventArgs e) => Do(entry =>
    {
        var camera = entry.Camera;
        if (camera.IsPaused)
        {
            camera.Resume();
            Say($"playing from {FormatTime(camera.Position)}.");
        }
        else
        {
            camera.Pause();
            Say($"paused at {FormatTime(camera.Position)}.");
        }
    }, "Play/Pause");

    private void OnVideoPicked(object? sender, EventArgs e)
    {
        if (_updating || _video.SelectedIndex < 0)
            return;

        int index = _video.SelectedIndex;
        Do(entry =>
        {
            entry.Camera.PlayItem(index);
            Say($"playing {entry.Playlist[index].Name} ({index + 1} of {entry.Playlist.Count}).");
        }, "Play video");
    }

    /// <summary>Back to the start of the video; pressed again at the start, to the video before.</summary>
    private void OnRewindClick(object? sender, EventArgs e) => Do(entry =>
    {
        int before = entry.Camera.ItemIndex;
        int now = entry.Camera.Rewind();
        Say(now == before
            ? $"rewound to the start of {entry.Playlist[now].Name}."
            : $"rewound to the start of the video before, {entry.Playlist[now].Name}.");
    }, "Rewind");

    private void OnNextClick(object? sender, EventArgs e) => Do(entry =>
    {
        int now = entry.Camera.Next();
        Say($"next video: {entry.Playlist[now].Name} ({now + 1} of {entry.Playlist.Count}).");
    }, "Next video");

    private void OnBack10Click(object? sender, EventArgs e) => SeekBy(-10);

    private void OnForward10Click(object? sender, EventArgs e) => SeekBy(10);

    private void SeekBy(int seconds) => Do(entry =>
    {
        entry.Camera.SeekBy(TimeSpan.FromSeconds(seconds));
        Say($"{(seconds < 0 ? "back" : "forward")} {Math.Abs(seconds)} s to {FormatTime(entry.Camera.Position)}.");
    }, seconds < 0 ? "Rewind" : "Forward");

    private void OnTimelineSeek(object? sender, double seconds) => Do(entry =>
    {
        entry.Camera.SeekTo(TimeSpan.FromSeconds(seconds));
        Say($"jumped to {FormatTime(entry.Camera.Position)}.");
    }, "Seek");

    private void OnSpeedChanged(object? sender, EventArgs e)
    {
        if (_updating || _speed.SelectedIndex < 0)
            return;

        Do(entry =>
        {
            entry.Camera.Speed = Speeds[_speed.SelectedIndex];
            Say($"speed {entry.Camera.Speed:0.##}x.");
        }, "Speed");
    }

    private void OnZoomInClick(object? sender, EventArgs e) => _timeline.ZoomIn();

    private void OnZoomOutClick(object? sender, EventArgs e) => _timeline.ZoomOut();

    private void OnZoomFitClick(object? sender, EventArgs e) => _timeline.ZoomToFit();

    // ===================================================================== skips

    private void OnSkipStartClick(object? sender, EventArgs e) => Do(entry =>
    {
        entry.PendingSkipVideo = entry.Video;
        entry.PendingSkipStart = entry.Camera.Position.TotalSeconds;
        Say($"skip starts at {FormatPrecise(entry.PendingSkipStart.Value)}. Move to where it should end and click Skip end.");
    }, "Skip start");

    private void OnSkipEndClick(object? sender, EventArgs e) => Do(entry =>
    {
        if (entry.PendingSkipStart is not double start || entry.PendingSkipVideo is not { } video)
            throw new InvalidOperationException("Click Skip start first, at the moment the skip should begin.");

        if (!ReferenceEquals(video, entry.Video))
            throw new InvalidOperationException(
                $"The skip was started in {video.Name}, but {entry.Video.Name} is playing now. Click Skip start again.");

        double end = entry.Camera.Position.TotalSeconds;
        entry.PendingSkipStart = null;
        entry.PendingSkipVideo = null;

        if (Math.Abs(end - start) < SkipRanges.MinLength)
            throw new InvalidOperationException("The skip is too short. Move the playhead further from where it starts.");

        video.SetSkips(video.Skips.Append(new SkipRange(Math.Min(start, end), Math.Max(start, end))));
    }, "Skip end");

    /// <summary>
    /// The user drew, moved or deleted skip ranges on this timeline. They belong to the video the
    /// timeline shows, even if the playlist has moved on to the next one meanwhile.
    /// </summary>
    private void OnTimelineSkipsChanged(object? sender, EventArgs e) =>
        Do(entry => (_shown ?? entry.Video).SetSkips(_timeline.SkipRanges), "Skip ranges");

    // ===================================================================== helpers

    private void Do(Action<CameraEntry> action, string context)
    {
        if (_host is null || Entry is null)
            return;

        var entry = Entry;
        _host.Run(() => action(entry), $"{context} ({entry.StreamPath})");
        UpdateView();
    }

    private async Task DoAsync(Func<CameraEntry, Task> action, string context)
    {
        if (_host is null || Entry is null)
            return;

        var entry = Entry;
        await _host.RunAsync(() => action(entry), $"{context} ({entry.StreamPath})");
        UpdateView();
    }

    private void Say(string message) => _host?.Log($"'{Entry?.StreamPath}': {message}");

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");

    private static string FormatPrecise(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, Math.Round(seconds, 1)));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss\.f") : time.ToString(@"mm\:ss\.f");
    }
}
