using System.Diagnostics;

namespace MockRtsp;

public sealed partial class MainForm : Form
{
    private sealed class CameraEntry
    {
        public CameraEntry(string streamPath, string sourceFile, MockCamera camera)
        {
            StreamPath = streamPath;
            SourceFile = sourceFile;
            Camera = camera;
        }

        public string StreamPath { get; }
        public string SourceFile { get; }
        public MockCamera Camera { get; }

        /// <summary>The VLC window opened for this camera, if any. Reopened after a settings change.</summary>
        public Process? Vlc { get; set; }

        public override string ToString()
        {
            string state = !Camera.IsOnline ? "[off] " : Camera.IsPaused ? "[ON]  paused  " : "[ON]  ";
            string speed = Camera.Speed == 1.0 ? string.Empty : $"  {Camera.Speed:0.##}x";
            return $"{state}{StreamPath}    ({Path.GetFileName(SourceFile)})    "
                   + $"{FormatTime(Camera.Position)} / {FormatTime(Camera.Duration)}{speed}";
        }
    }

    // Found in the "mediamtx" and "ffmpeg" folders next to the program when the form loads. Null if missing.
    private string? _mediaMtxExe;
    private string? _ffmpegExe;

    private StreamOptions _options = new();         // Saved in options.json. Loaded in OnLoad.
    private StreamOptions? _serverOptions;          // What the running server was started with.
    private readonly List<CameraEntry> _entries = new();
    private MediaMtxServer? _server;
    private bool _busy;

    // Player row state.
    private static readonly double[] Speeds = { 0.25, 0.5, 1.0, 2.0, 4.0 };     // matches the _speed items

    // Thumbnails per video file, shared by cameras that play the same file. Made in the background.
    private readonly Dictionary<string, ThumbnailStrip> _thumbnails = new(StringComparer.OrdinalIgnoreCase);

    // Skip ranges per video file, shared by cameras that play the same file and saved next to it.
    private readonly Dictionary<string, IReadOnlyList<SkipRange>> _skips = new(StringComparer.OrdinalIgnoreCase);

    // A skip range started with "Skip start" and waiting for "Skip end".
    private (string File, double Start)? _pendingSkip;
    private bool _updatingPlayer;       // set while code (not the user) changes the player controls

    public MainForm()
    {
        InitializeComponent();

        // Owner-drawn rows do not scale with DPI by themselves, so size them from the actual font.
        _cameras.ItemHeight = _cameras.Font.Height + 6;
    }

    private CameraEntry? SelectedEntry => _cameras.SelectedItem as CameraEntry;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // The designer runs this form too, and must not look for tools or start the timer.
        if (DesignMode)
            return;

        _options = OptionsStore.Load();

        // Auto setup: look for the bundled tools once, at startup.
        _mediaMtxExe = Toolchain.Find(Toolchain.MediaMtxFolder, "mediamtx.exe");
        _ffmpegExe = Toolchain.Find(Toolchain.FfmpegFolder, "ffmpeg.exe");

        _timer.Start();
        UpdateButtons();

        Log(_mediaMtxExe is not null
            ? $"MediaMTX: {_mediaMtxExe}"
            : $"MediaMTX not found. Put mediamtx.exe in the '{Toolchain.MediaMtxFolder}' folder next to MockRtsp.exe.");
        Log(_ffmpegExe is not null
            ? $"FFmpeg: {_ffmpegExe}"
            : $"FFmpeg not found. Put ffmpeg.exe in the '{Toolchain.FfmpegFolder}' folder next to MockRtsp.exe.");
        RefreshRecentFiles();
        Log("Start the MediaMTX server, then add cameras.");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!DesignMode)
        {
            Safe(StopServer, "Shutdown");

            foreach (var strip in _thumbnails.Values)
                strip.Dispose();
            _thumbnails.Clear();
        }

        base.OnFormClosing(e);
    }

    // ---- Event handlers (wired in MainForm.Designer.cs) ----

    private void OnBrowseVideoClick(object? sender, EventArgs e) => Safe(BrowseVideo, "Browse for video");

    private async void OnStartServerClick(object? sender, EventArgs e) =>
        await SafeAsync(StartServerAsync, "Start server");

    private void OnStopServerClick(object? sender, EventArgs e) => Safe(StopServer, "Stop server");

    private async void OnOptionsClick(object? sender, EventArgs e) =>
        await SafeAsync(EditSettingsAsync, "Settings");

    private void OnAddCameraClick(object? sender, EventArgs e) => Safe(AddCamera, "Add camera");

    private async void OnOpenVlcClick(object? sender, EventArgs e) =>
        await SafeAsync(OpenInVlcAsync, "Open in VLC");

    private void OnStartCameraClick(object? sender, EventArgs e) => Safe(StartSelectedCamera, "Start camera");

    private void OnStopCameraClick(object? sender, EventArgs e) => Safe(StopSelectedCamera, "Stop camera");

    private void OnRemoveCameraClick(object? sender, EventArgs e) => Safe(RemoveSelectedCamera, "Remove camera");

    private async void OnOutageClick(object? sender, EventArgs e) =>
        await SafeAsync(SimulateOutageAsync, "Simulate outage");

    private void OnCamerasSelectedIndexChanged(object? sender, EventArgs e) => UpdateButtons();

    /// <summary>
    /// Draws each camera row from its current state. A normal ListBox keeps the text it had
    /// when the item was added, so the [ON]/[off] label would never change.
    /// </summary>
    private void OnCamerasDrawItem(object? sender, DrawItemEventArgs e)
    {
        try
        {
            e.DrawBackground();

            if (e.Index >= 0 && e.Index < _cameras.Items.Count)
            {
                string text = _cameras.Items[e.Index]?.ToString() ?? string.Empty;
                TextRenderer.DrawText(e.Graphics, text, e.Font ?? _cameras.Font, e.Bounds, e.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            e.DrawFocusRectangle();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Drawing camera list");
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (_server is not null && !_server.IsRunning)
            {
                Log("MediaMTX stopped unexpectedly. All cameras were stopped.");
                StopServer();
            }

            _cameras.Invalidate();      // refresh [ON]/[off] labels
            UpdateButtons();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Status timer");
        }
    }

    // ---- Error handling wrappers: every UI action goes through one of these ----

    private void Safe(Action action, string context)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            HandleError(ex, context);
        }
    }

    private async Task SafeAsync(Func<Task> action, string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            HandleError(ex, context);
        }
    }

    private void HandleError(Exception ex, string context)
    {
        ErrorReporter.Report(ex, context);
        Log($"ERROR ({context}): {ex.Message}");
        MessageBox.Show(this, ex.Message, context, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    // ---- Tool checks ----

    private string RequireMediaMtx()
    {
        if (_mediaMtxExe is null || !File.Exists(_mediaMtxExe))
            throw new FileNotFoundException(
                $"mediamtx.exe not found. Put it in the '{Toolchain.MediaMtxFolder}' folder next to MockRtsp.exe.");

        return _mediaMtxExe;
    }

    private string RequireFfmpeg()
    {
        if (_ffmpegExe is null || !File.Exists(_ffmpegExe))
            throw new FileNotFoundException(
                $"ffmpeg.exe not found. Put it in the '{Toolchain.FfmpegFolder}' folder next to MockRtsp.exe.");

        return _ffmpegExe;
    }

    // ---- Actions ----

    private void BrowseVideo()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a video to loop",
            Filter = "Video (*.mp4;*.mkv;*.ts;*.h264;*.h265)|*.mp4;*.mkv;*.ts;*.h264;*.h265|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _sourceFile.Text = dialog.FileName;
    }

    private async Task StartServerAsync()
    {
        if (_server is not null)
        {
            Log("Server is already running.");
            return;
        }

        string exe = RequireMediaMtx();
        int port = (int)_port.Value;

        var started = _options.Clone();

        await RunBusyAsync(async () =>
        {
            _server = await MediaMtxServer.StartAsync(port, exe, log: Log, options: started);
            _serverOptions = started;
        });

        Log($"MediaMTX listening on rtsp://127.0.0.1:{port}");
        UpdateButtons();
    }

    private void StopServer()
    {
        // Stop and dispose every camera first, so no publisher outlives the server.
        foreach (var entry in _entries.ToList())
            DisposeCamera(entry);

        _entries.Clear();
        _cameras.Items.Clear();

        var server = _server;
        _server = null;
        _serverOptions = null;

        try
        {
            server?.Dispose();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Stopping server");
        }

        if (server is not null)
            Log("MediaMTX stopped.");

        UpdateButtons();
    }

    private void AddCamera()
    {
        if (_server is null)
            throw new InvalidOperationException("Start the MediaMTX server first.");

        string path = _streamPath.Text.Trim().Trim('/');

        if (path.Length == 0)
            throw new InvalidOperationException("Enter a stream path, for example Streaming/Channels/101.");

        if (path.Any(c => char.IsWhiteSpace(c) || c is '?' or '#'))
            throw new InvalidOperationException("The stream path cannot contain spaces, '?' or '#'.");

        if (_entries.Any(e => e.StreamPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A camera with path '{path}' already exists.");

        string source = _sourceFile.Text.Trim();

        if (source.Length == 0)
            throw new InvalidOperationException("Choose a video file to loop.");

        if (!File.Exists(source))
            throw new FileNotFoundException("Video file not found.", source);

        var camera = new MockCamera(_server, path, source, RequireFfmpeg(), CameraOptions());
        camera.PublisherExited += (_, code) =>
        {
            string output = camera.LastFfmpegOutput;
            Log($"FFmpeg for '{path}' exited with code {code}. Stream is down."
                + (output.Length > 0 ? Environment.NewLine + output : string.Empty));
        };

        var entry = new CameraEntry(path, source, camera);
        _entries.Add(entry);
        _cameras.Items.Add(entry);
        _cameras.SelectedItem = entry;

        // Thumbnails for the timeline, made in the background. Shared by cameras playing the same file.
        if (!_thumbnails.ContainsKey(source))
            _thumbnails[source] = ThumbnailStrip.Start(RequireFfmpeg(), source, camera.Source);

        // Skip ranges saved next to the video come back with it.
        if (!_skips.TryGetValue(source, out var skips))
        {
            skips = LoadSkips(source, camera.Duration.TotalSeconds);
            _skips[source] = skips;
        }
        camera.SkipRanges = skips;

        RecentFiles.Add(source);
        RefreshRecentFiles();

        Log($"Added camera {camera.Url}");

        // A camera is live as soon as it is added. If FFmpeg cannot start, the camera stays
        // in the list (stopped) and the error is shown.
        try
        {
            camera.Start();
            Log($"Started {camera.Url}");
        }
        finally
        {
            UpdateButtons();
        }
    }

    private async Task OpenInVlcAsync()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");

        if (!entry.Camera.IsOnline)
            throw new InvalidOperationException("Start the camera first, then open it in VLC.");

        // Opening VLC before MediaMTX serves the stream makes VLC show an error, so wait for it.
        bool live = false;
        await RunBusyAsync(async () => live = await entry.Camera.WaitUntilLiveAsync(TimeSpan.FromSeconds(10)));

        if (!live)
            throw new InvalidOperationException(
                $"The stream for '{entry.StreamPath}' is not playing. Check the log for FFmpeg errors.");

        OpenVlc(entry);
        Log($"Opened {entry.Camera.Url} in VLC.");
    }

    /// <summary>Opens VLC for the camera, closing the window it had open before.</summary>
    private void OpenVlc(CameraEntry entry)
    {
        string vlc = VlcLauncher.Find()
            ?? throw new FileNotFoundException(
                "VLC was not found. Install VLC, or put vlc.exe in a 'vlc' folder next to MockRtsp.exe.");

        VlcLauncher.Close(entry.Vlc);
        entry.Vlc = VlcLauncher.Open(vlc, entry.Camera.ViewerUrl, entry.StreamPath);
    }

    /// <summary>
    /// Restarts a running camera with the current settings, waits until the new stream plays,
    /// and reopens its VLC window. Viewers are disconnected by the restart, just as with a real
    /// camera that changes resolution, and VLC does not reconnect by itself.
    /// </summary>
    private async Task ApplySettingsToCameraAsync(CameraEntry entry)
    {
        try
        {
            bool hadVlc = VlcLauncher.IsOpen(entry.Vlc);
            bool wasOnline = entry.Camera.IsOnline;

            entry.Camera.ApplyOptions(CameraOptions());

            if (!wasOnline)
            {
                Log($"'{entry.StreamPath}': saved. It uses the new settings when started.");
                return;
            }

            if (!await entry.Camera.WaitUntilLiveAsync(TimeSpan.FromSeconds(10)))
            {
                Log($"'{entry.StreamPath}': the stream did not come back after the change. See the FFmpeg lines above.");
                return;
            }

            Log($"'{entry.StreamPath}': now streaming {Describe(entry.Camera.Format, entry.Camera.Options)}.");

            if (hadVlc)
            {
                OpenVlc(entry);
                Log($"'{entry.StreamPath}': VLC reopened on the new stream.");
            }
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, $"Applying settings to {entry.StreamPath}");
            Log($"ERROR: could not apply the settings to '{entry.StreamPath}': {ex.Message}");
        }
    }

    /// <summary>What a camera actually sends, e.g. "1280x720, 15 fps, 2000 kbps".</summary>
    private static string Describe(VideoFormat format, StreamOptions o)
    {
        string bitrate = o.BitrateKbps > 0 ? ", " + o.BitrateKbps + " kbps" : string.Empty;
        return $"{format.Width}x{format.Height}, {format.FrameRate} fps{bitrate}";
    }

    /// <summary>The settings themselves, e.g. "original size, 15 fps".</summary>
    private static string Describe(StreamOptions o)
    {
        string size = o.Width > 0 ? o.Width + "x" + o.Height : "original size";
        string rate = o.FrameRate > 0 ? o.FrameRate + " fps" : "original frame rate";
        string bitrate = o.BitrateKbps > 0 ? ", " + o.BitrateKbps + " kbps" : string.Empty;
        return size + ", " + rate + bitrate;
    }

    /// <summary>With tenths of a second, for skip edges, e.g. "01:23.4".</summary>
    private static string FormatPrecise(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, Math.Round(seconds, 1)));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss\.f") : time.ToString(@"mm\:ss\.f");
    }

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");

    // ---- Player: pause, rewind, forward, seek and speed for the selected camera ----

    private void OnPlayPauseClick(object? sender, EventArgs e) => Safe(TogglePause, "Play/Pause");

    private void OnBack10Click(object? sender, EventArgs e) => Safe(() => SeekSelectedBy(-10), "Rewind");

    private void OnForward10Click(object? sender, EventArgs e) => Safe(() => SeekSelectedBy(10), "Forward");

    private void OnTimelineSeek(object? sender, double seconds) => Safe(() =>
    {
        var entry = SelectedEntry;
        if (entry is null)
            return;

        entry.Camera.SeekTo(TimeSpan.FromSeconds(seconds));
        Log($"'{entry.StreamPath}': jumped to {FormatTime(entry.Camera.Position)}.");
        UpdatePlayer();
    }, "Seek");

    private void OnZoomInClick(object? sender, EventArgs e) => _timeline.ZoomIn();

    private void OnZoomOutClick(object? sender, EventArgs e) => _timeline.ZoomOut();

    private void OnZoomFitClick(object? sender, EventArgs e) => _timeline.ZoomToFit();

    // ---- Skip ranges: parts of a video the cameras jump over, saved next to the video ----

    private void OnSkipStartClick(object? sender, EventArgs e) => Safe(() =>
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");
        double start = entry.Camera.Position.TotalSeconds;

        _pendingSkip = (entry.SourceFile, start);
        Log($"'{entry.StreamPath}': skip starts at {FormatPrecise(start)}. Move to where it should end and click Skip end.");
        UpdatePlayer();
    }, "Skip start");

    private void OnSkipEndClick(object? sender, EventArgs e) => Safe(() =>
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");

        if (_pendingSkip is not { } pending || !string.Equals(pending.File, entry.SourceFile, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Click Skip start first, at the moment the skip should begin.");

        double end = entry.Camera.Position.TotalSeconds;
        _pendingSkip = null;

        if (Math.Abs(end - pending.Start) < SkipRanges.MinLength)
            throw new InvalidOperationException("The skip is too short. Move the playhead further from where it starts.");

        var current = _skips.TryGetValue(entry.SourceFile, out var list) ? list : Array.Empty<SkipRange>();
        SetSkips(entry.SourceFile, current.Append(new SkipRange(Math.Min(pending.Start, end), Math.Max(pending.Start, end))),
            entry.Camera.Duration.TotalSeconds);
        UpdatePlayer();
    }, "Skip end");

    /// <summary>The user drew, moved or deleted skip ranges on the timeline.</summary>
    private void OnTimelineSkipsChanged(object? sender, EventArgs e) => Safe(() =>
    {
        var entry = SelectedEntry;
        if (entry is not null)
            SetSkips(entry.SourceFile, _timeline.SkipRanges, entry.Camera.Duration.TotalSeconds);
    }, "Skip ranges");

    /// <summary>
    /// Applies a video's skip ranges to every camera playing it, and saves them next to the video.
    /// </summary>
    private void SetSkips(string file, IEnumerable<SkipRange> ranges, double duration)
    {
        var normalized = SkipRanges.Normalize(ranges, duration);
        _skips[file] = normalized;

        foreach (var entry in _entries.Where(x => string.Equals(x.SourceFile, file, StringComparison.OrdinalIgnoreCase)))
            entry.Camera.SkipRanges = normalized;

        if (!SkipRanges.LeavesSomethingToPlay(normalized, duration))
            Log("Every part of the video is marked to skip, so the skips are ignored until some are removed.");

        try
        {
            SkipRangeFile.Save(file, normalized);
            Log(normalized.Count == 0
                ? $"No skips left for {Path.GetFileName(file)}; removed {Path.GetFileName(SkipRangeFile.PathFor(file))}."
                : $"Saved {normalized.Count} skip(s) to {Path.GetFileName(SkipRangeFile.PathFor(file))}: "
                  + string.Join(", ", normalized.Select(r => $"{FormatPrecise(r.Start)}-{FormatPrecise(r.End)}")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorReporter.Report(ex, $"Saving skips for {file}");
            Log($"ERROR: the skips are in use but could not be saved next to the video ({ex.Message}). "
                + "They will be lost when the app closes. Check that the video's folder is writable.");
        }

        _timeline.SetSkipRanges(normalized);
    }

    private List<SkipRange> LoadSkips(string file, double duration)
    {
        try
        {
            var ranges = SkipRangeFile.Load(file, duration);
            if (ranges.Count > 0)
                Log($"Loaded {ranges.Count} skip(s) from {Path.GetFileName(SkipRangeFile.PathFor(file))}.");
            return ranges;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorReporter.Report(ex, $"Loading skips for {file}");
            Log($"ERROR: could not read the skips saved next to the video ({ex.Message}). Playing without skips; "
                + "the file is left as it is until you change the skips.");
            return new List<SkipRange>();
        }
    }

    private void OnSpeedChanged(object? sender, EventArgs e)
    {
        if (_updatingPlayer)
            return;

        Safe(() =>
        {
            var entry = SelectedEntry;
            if (entry is null || _speed.SelectedIndex < 0)
                return;

            entry.Camera.Speed = Speeds[_speed.SelectedIndex];
            Log($"'{entry.StreamPath}': speed {entry.Camera.Speed:0.##}x.");
        }, "Speed");
    }

    private void TogglePause()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");
        var camera = entry.Camera;

        if (camera.IsPaused)
        {
            camera.Resume();
            Log($"'{entry.StreamPath}': playing from {FormatTime(camera.Position)}.");
        }
        else
        {
            camera.Pause();
            Log($"'{entry.StreamPath}': paused at {FormatTime(camera.Position)}.");
        }

        UpdatePlayer();
    }

    private void SeekSelectedBy(int seconds)
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");

        entry.Camera.SeekBy(TimeSpan.FromSeconds(seconds));
        Log($"'{entry.StreamPath}': {(seconds < 0 ? "back" : "forward")} {Math.Abs(seconds)} s to {FormatTime(entry.Camera.Position)}.");
        UpdatePlayer();
    }

    /// <summary>Shows the selected camera's position, pause state and speed in the player row.</summary>
    private void UpdatePlayer()
    {
        var entry = SelectedEntry;
        bool enabled = !_busy && entry is not null;

        _back10.Enabled = enabled;
        _playPause.Enabled = enabled;
        _forward10.Enabled = enabled;
        _speed.Enabled = enabled;
        _zoomOut.Enabled = entry is not null;
        _zoomIn.Enabled = entry is not null;
        _zoomFit.Enabled = entry is not null;
        _skipStart.Enabled = enabled;
        _skipEnd.Enabled = enabled && _pendingSkip is not null;
        _timeline.Enabled = enabled;

        if (entry is null)
        {
            _timeLabel.Text = "--:-- / --:--";
            _timeline.SetSource(null, 0);
            return;
        }

        var camera = entry.Camera;
        _updatingPlayer = true;
        try
        {
            _playPause.Text = camera.IsPaused ? "Play" : "Pause";

            _timeLabel.Text = $"{FormatTime(camera.Position)} / {FormatTime(camera.Duration)}";

            _thumbnails.TryGetValue(entry.SourceFile, out var strip);
            _timeline.SetSource(strip, camera.Duration.TotalSeconds);
            _timeline.SetSkipRanges(_skips.TryGetValue(entry.SourceFile, out var skips) ? skips : Array.Empty<SkipRange>());
            _timeline.PendingStart = _pendingSkip is { } pending
                && string.Equals(pending.File, entry.SourceFile, StringComparison.OrdinalIgnoreCase) ? pending.Start : null;
            _timeline.Position = camera.Position.TotalSeconds;
            _timeline.Invalidate();             // new thumbnails may have arrived

            int speedIndex = Array.IndexOf(Speeds, camera.Speed);
            if (_speed.SelectedIndex != speedIndex)
                _speed.SelectedIndex = speedIndex;
        }
        finally
        {
            _updatingPlayer = false;
        }
    }

    private void RefreshRecentFiles()
    {
        string current = _sourceFile.Text;

        _sourceFile.Items.Clear();
        _sourceFile.Items.AddRange(RecentFiles.Load().ToArray());
        _sourceFile.Text = current;
    }

    private void StartSelectedCamera()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");

        entry.Camera.Start();
        Log($"Started {entry.Camera.Url}");
        UpdateButtons();
    }

    private void StopSelectedCamera()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");

        entry.Camera.Stop();
        Log($"Stopped {entry.Camera.Url}");
        UpdateButtons();
    }

    private void RemoveSelectedCamera()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");

        DisposeCamera(entry);
        _entries.Remove(entry);
        _cameras.Items.Remove(entry);

        // Drop the thumbnails unless another camera plays the same file.
        if (!_entries.Any(other => string.Equals(other.SourceFile, entry.SourceFile, StringComparison.OrdinalIgnoreCase))
            && _thumbnails.TryGetValue(entry.SourceFile, out var strip))
        {
            _thumbnails.Remove(entry.SourceFile);
            _skips.Remove(entry.SourceFile);
            strip.Dispose();
        }

        Log($"Removed {entry.StreamPath}");
        UpdateButtons();
    }

    private async Task SimulateOutageAsync()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");
        int seconds = (int)_outageSeconds.Value;

        Log($"Simulating outage on '{entry.StreamPath}' for {seconds} s...");

        bool hadVlc = VlcLauncher.IsOpen(entry.Vlc);
        bool live = false;

        await RunBusyAsync(async () =>
        {
            await entry.Camera.SimulateOutageAsync(TimeSpan.FromSeconds(seconds));
            live = await entry.Camera.WaitUntilLiveAsync(TimeSpan.FromSeconds(10));
        });

        Log(live
            ? $"Outage on '{entry.StreamPath}' ended. The stream is back."
            : $"Outage on '{entry.StreamPath}' ended, but the stream did not come back. See the FFmpeg lines above.");

        // VLC gives up during the outage and does not reconnect, so reopen it.
        if (live && hadVlc)
            OpenVlc(entry);

        UpdateButtons();
    }

    /// <summary>
    /// The one place to change stream settings. Saving applies them to every camera:
    /// running cameras restart with the new settings and their VLC windows reopen.
    /// </summary>
    private async Task EditSettingsAsync()
    {
        StreamOptions edited;
        using (var dialog = new OptionsDialog(_options))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            edited = dialog.Result;
        }

        _options = edited;

        if (!OptionsStore.Save(_options))
            Log("WARNING: the settings could not be saved to disk, so they will be lost when the app closes.");

        if (_entries.Count == 0)
        {
            Log($"Settings saved: {Describe(_options)}. Cameras you add will use them.");
        }
        else
        {
            Log($"Settings saved: {Describe(_options)}. Applying to {_entries.Count} camera(s)...");
            await RunBusyAsync(async () =>
            {
                foreach (var entry in _entries.ToList())
                    await ApplySettingsToCameraAsync(entry);
            });
        }

        if (_server is not null && _serverOptions is not null && !_serverOptions.ServerSettingsEqual(_options))
            Log("The server settings (transports, timeouts, logins, log level) changed. " +
                "Stop and start the server for them to take effect.");

        UpdateButtons();
    }

    /// <summary>
    /// The options for a camera: the saved options, but with the logins the running server
    /// was started with, because changed logins would be refused until the server restarts.
    /// </summary>
    private StreamOptions CameraOptions()
    {
        var options = _options.Clone();

        if (_serverOptions is not null)
        {
            options.ViewerUser = _serverOptions.ViewerUser;
            options.ViewerPassword = _serverOptions.ViewerPassword;
            options.PublisherUser = _serverOptions.PublisherUser;
            options.PublisherPassword = _serverOptions.PublisherPassword;
        }

        return options;
    }

    private void DisposeCamera(CameraEntry entry)
    {
        // The VLC window would only show a dead stream, so close it with the camera.
        VlcLauncher.Close(entry.Vlc);
        entry.Vlc = null;

        try
        {
            entry.Camera.Dispose();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, $"Disposing camera {entry.StreamPath}");
        }
    }

    private async Task RunBusyAsync(Func<Task> work)
    {
        _busy = true;
        UpdateButtons();

        try
        {
            await work();
        }
        finally
        {
            _busy = false;
            UpdateButtons();
        }
    }

    private void UpdateButtons()
    {
        bool serverUp = _server is not null;
        var selected = SelectedEntry;
        bool selectedOnline = selected?.Camera.IsOnline ?? false;

        _port.Enabled = !serverUp && !_busy;
        _optionsButton.Enabled = !_busy;
        _startServer.Enabled = !serverUp && !_busy && _mediaMtxExe is not null;
        _stopServer.Enabled = serverUp && !_busy;

        _streamPath.Enabled = serverUp && !_busy;
        _sourceFile.Enabled = serverUp && !_busy;
        _browseVideo.Enabled = serverUp && !_busy;
        _addCamera.Enabled = serverUp && !_busy;

        _startCamera.Enabled = !_busy && selected is not null && !selectedOnline;
        _stopCamera.Enabled = !_busy && selected is not null && selectedOnline;
        _removeCamera.Enabled = !_busy && selected is not null;
        _openVlc.Enabled = !_busy && selectedOnline;
        _outage.Enabled = !_busy && selected is not null;

        _statusText.Text = serverUp
            ? $"MediaMTX running on port {_server!.Port}  |  cameras: {_entries.Count}"
            : _mediaMtxExe is null ? "MediaMTX not found" : "Server stopped";

        UpdatePlayer();
    }

    private void Log(string message)
    {
        if (IsDisposed)
            return;

        // Background threads (FFmpeg exit notifications) must marshal to the UI thread.
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => Log(message)));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // Form is closing.
            }
            return;
        }

        if (_log.TextLength > 200_000)
            _log.Clear();

        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
