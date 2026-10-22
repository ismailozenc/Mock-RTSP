namespace MockRtsp;

/// <summary>
/// The main window: the server, the video folder with its list of playable videos, and one
/// panel per camera. The layout lives in MainForm.Designer.cs.
/// </summary>
public sealed partial class MainForm : Form, ICameraHost
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".mkv", ".avi", ".ts", ".mts", ".m2ts", ".flv", ".wmv", ".webm",
        ".h264", ".264", ".h265", ".265", ".hevc"
    };

    // Found in the "mediamtx" and "ffmpeg" folders next to the program when the form loads. Null if missing.
    private string? _mediaMtxExe;
    private string? _ffmpegExe;

    private StreamOptions _options = new();         // Saved in options.json. Loaded in OnLoad.
    private StreamOptions? _serverOptions;          // What the running server was started with.
    private MediaMtxServer? _server;
    private bool _busy;

    private readonly List<CameraEntry> _entries = new();
    private readonly Dictionary<CameraEntry, CameraPanel> _panels = new();

    // Videos in use, by full path. Cameras playing the same file share one.
    private readonly Dictionary<string, VideoFile> _videos = new(StringComparer.OrdinalIgnoreCase);

    private string _currentFolder = string.Empty;
    private int _folderGeneration;                  // bumped per folder load, so late results of an old one are dropped

    public MainForm()
    {
        InitializeComponent();
    }

    // ===================================================================== ICameraHost

    public bool Busy => _busy;

    public StreamOptions Defaults => _options;

    public void Run(Action action, string context) => Safe(action, context);

    public Task RunAsync(Func<Task> action, string context) => SafeAsync(action, context);

    // ===================================================================== start and close

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

        SetSplitters();
        _timer.Start();

        Log(_mediaMtxExe is not null
            ? $"MediaMTX: {_mediaMtxExe}"
            : $"MediaMTX not found. Put mediamtx.exe in the '{Toolchain.MediaMtxFolder}' folder next to MockRtsp.exe.");
        Log(_ffmpegExe is not null
            ? $"FFmpeg: {_ffmpegExe}"
            : $"FFmpeg not found. Put ffmpeg.exe in the '{Toolchain.FfmpegFolder}' folder next to MockRtsp.exe.");

        RefreshFolderList();
        if (_options.VideoFolder.Length > 0 && Directory.Exists(_options.VideoFolder))
            Safe(() => LoadFolder(_options.VideoFolder), "Video folder");

        Log("Start the server, then double-click a video to add it as a camera.");
        UpdateButtons();
    }

    /// <summary>Videos list about half the width; the log a few lines high.</summary>
    private void SetSplitters()
    {
        try
        {
            _content.SplitterDistance = Math.Max(_content.Panel1MinSize, Math.Min(610, _content.Width * 2 / 5));
            _split.SplitterDistance = Math.Max(_split.Panel1MinSize, _split.Height - Math.Max(120, _split.Height / 6));
        }
        catch (InvalidOperationException)
        {
            // The window is too small for these sizes; keep the defaults.
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!DesignMode)
        {
            _folderGeneration++;                        // drop any folder results still on their way
            Safe(StopServer, "Shutdown");
        }

        base.OnFormClosing(e);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (_server is not null && !_server.IsRunning)
            {
                Log("MediaMTX stopped unexpectedly. All cameras were removed.");
                StopServer();
            }

            foreach (var video in _videos.Values.ToList())
                video.Poll(_options);                   // applies finished motion scans

            foreach (var panel in _panels.Values)
                panel.UpdateView();

            LayoutPanels();
            UpdateButtons();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Status timer");
        }
    }

    // ===================================================================== event handlers

    private async void OnStartServerClick(object? sender, EventArgs e) => await SafeAsync(StartServerAsync, "Start server");

    private void OnStopServerClick(object? sender, EventArgs e) => Safe(StopServer, "Stop server");

    private async void OnOptionsClick(object? sender, EventArgs e) => await SafeAsync(EditSettingsAsync, "Settings");

    private void OnAddCameraClick(object? sender, EventArgs e) => Safe(AddCamera, "Add camera");

    private void OnFilesSelectionChanged(object? sender, EventArgs e) => UpdateButtons();

    private void OnCamerasResize(object? sender, EventArgs e) => LayoutPanels();

    private void OnFolderPicked(object? sender, EventArgs e) =>
        Safe(() => LoadFolder(_folder.SelectedItem?.ToString() ?? _folder.Text), "Video folder");

    private void OnFolderKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        Safe(() => LoadFolder(_folder.Text), "Video folder");
    }

    private void OnBrowseFolderClick(object? sender, EventArgs e) => Safe(() =>
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the folder with your camera videos",
            SelectedPath = Directory.Exists(_currentFolder) ? _currentFolder : string.Empty
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadFolder(dialog.SelectedPath);
    }, "Video folder");

    private void OnRefreshFolderClick(object? sender, EventArgs e) =>
        Safe(() => LoadFolder(_currentFolder.Length > 0 ? _currentFolder : _folder.Text), "Video folder");

    // ===================================================================== error handling

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

    // ===================================================================== video folder

    /// <summary>Lists the video files of a folder, then checks each in the background and keeps the playable ones.</summary>
    private void LoadFolder(string folder)
    {
        folder = folder.Trim();
        if (folder.Length == 0)
            return;

        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"The folder '{folder}' does not exist.");

        folder = Path.GetFullPath(folder);
        _currentFolder = folder;
        int generation = ++_folderGeneration;
        RememberFolder(folder);

        var files = Directory.EnumerateFiles(folder)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        _files.BeginUpdate();
        try
        {
            _files.Items.Clear();
            foreach (var file in files)
            {
                var item = new ListViewItem(new[] { Path.GetFileName(file), "...", "...", "checking", string.Empty })
                {
                    Tag = file,
                    ForeColor = SystemColors.GrayText
                };
                _files.Items.Add(item);
                UpdateCameraColumn(item);
            }
        }
        finally
        {
            _files.EndUpdate();
        }

        _folderInfo.Text = files.Count == 0 ? "No video files in this folder." : $"Checking {files.Count} file(s)...";
        Log($"Video folder: {folder} ({files.Count} video file(s)).");

        if (files.Count > 0)
            _ = ProbeFolderAsync(files, RequireFfmpeg(), generation);
    }

    /// <summary>Reads each file's length and picture size, two at a time, and hides files that are not playable.</summary>
    private async Task ProbeFolderAsync(List<string> files, string ffmpeg, int generation)
    {
        using var gate = new SemaphoreSlim(2);
        var unplayable = new List<string>();
        int playable = 0;

        var work = files.Select(async file =>
        {
            await gate.WaitAsync();
            try
            {
                if (generation != _folderGeneration)
                    return;

                var (info, saved, error) = await Task.Run(() => ProbeFile(ffmpeg, file));

                // Back on the UI thread here.
                if (generation != _folderGeneration || IsDisposed)
                    return;

                if (info is null)
                {
                    unplayable.Add($"{Path.GetFileName(file)} ({error})");
                    FindItem(file)?.Remove();
                }
                else
                {
                    playable++;
                    ShowFileDetails(file, info, saved);
                }
            }
            finally
            {
                gate.Release();
            }
        });

        try
        {
            await Task.WhenAll(work);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Checking the video folder");
        }

        if (generation != _folderGeneration || IsDisposed)
            return;

        _folderInfo.Text = $"{playable} playable video(s)" + (unplayable.Count > 0 ? $", {unplayable.Count} other file(s) hidden" : string.Empty) + ".";
        if (unplayable.Count > 0)
            Log("Not playable, so not listed: " + string.Join("; ", unplayable));
    }

    private static (VideoInfo? Info, SavedVideo? Saved, string? Error) ProbeFile(string ffmpeg, string file)
    {
        try
        {
            var info = VideoInfo.Probe(ffmpeg, file);
            SavedVideo? saved = null;
            try
            {
                saved = VideoSidecar.Load(file, info.DurationSeconds);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // Shown as unknown in the list; opening the video reports it properly.
            }

            return (info, saved, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return (null, null, ex.Message.Split('\n')[0].Trim());
        }
    }

    private void ShowFileDetails(string file, VideoInfo info, SavedVideo? saved)
    {
        var item = FindItem(file);
        if (item is null)
            return;

        item.ForeColor = SystemColors.WindowText;
        item.SubItems[1].Text = FormatTime(TimeSpan.FromSeconds(info.DurationSeconds));
        item.SubItems[2].Text = $"{info.Width}x{info.Height} {info.FrameRate:0.##} fps";

        if (_videos.TryGetValue(file, out var video))
            item.SubItems[3].Text = DescribeSaved(video.Skips, video.Motion is not null, video.HasOwnSettings);
        else
            item.SubItems[3].Text = saved is null ? "?" : DescribeSaved(saved.Skips, saved.Scanned, saved.Settings is not null);
    }

    /// <summary>Refreshes a video's row: its skips and the cameras playing it.</summary>
    private void UpdateFileItem(string file)
    {
        var item = FindItem(file);
        if (item is null)
            return;

        if (_videos.TryGetValue(file, out var video))
            item.SubItems[3].Text = DescribeSaved(video.Skips, video.Motion is not null, video.HasOwnSettings);

        UpdateCameraColumn(item);
    }

    private void UpdateCameraColumn(ListViewItem item)
    {
        string file = (string)item.Tag!;
        item.SubItems[4].Text = string.Join(", ", _entries
            .Where(e => string.Equals(e.Video.Path, Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
            .Select(e => e.StreamPath));
    }

    private ListViewItem? FindItem(string file)
    {
        string full = Path.GetFullPath(file);
        return _files.Items.Cast<ListViewItem>()
            .FirstOrDefault(i => string.Equals(Path.GetFullPath((string)i.Tag!), full, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeSaved(IReadOnlyList<SkipRange> skips, bool scanned, bool ownSettings)
    {
        string text = skips.Count > 0
            ? $"{skips.Count} skip(s), {FormatTime(TimeSpan.FromSeconds(skips.Sum(r => r.Length)))} cut"
            : scanned ? "scanned, nothing cut" : "not scanned";

        return ownSettings ? text + ", own settings" : text;
    }

    private void RememberFolder(string folder)
    {
        _options.VideoFolder = folder;
        _options.RecentFolders = new[] { folder }
            .Concat(_options.RecentFolders.Where(f => !string.Equals(f, folder, StringComparison.OrdinalIgnoreCase)))
            .Take(10)
            .ToArray();

        if (!OptionsStore.Save(_options))
            Log("WARNING: could not save the settings, so the folder is not remembered.");

        RefreshFolderList();
    }

    private void RefreshFolderList()
    {
        _folder.Items.Clear();
        _folder.Items.AddRange(_options.RecentFolders.Cast<object>().ToArray());
        _folder.Text = _currentFolder.Length > 0 ? _currentFolder : _options.VideoFolder;
    }

    // ===================================================================== server

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
        // Remove every camera first, so no publisher outlives the server.
        foreach (var entry in _entries.ToList())
            RemoveCameraQuietly(entry);

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

        LayoutPanels();
        UpdateButtons();
    }

    // ===================================================================== cameras

    private void AddCamera()
    {
        if (_server is null)
            throw new InvalidOperationException("Start the server first.");

        if (_files.SelectedItems.Count == 0)
            throw new InvalidOperationException("Pick a video in the list first.");

        string file = (string)_files.SelectedItems[0].Tag!;
        string path = CheckStreamPath(_streamPath.Text);

        var video = OpenVideo(file);
        MockCamera camera;
        try
        {
            camera = new MockCamera(_server, path, video.Path, RequireFfmpeg(), CameraOptions(video));
        }
        catch
        {
            ReleaseVideoIfUnused(video);
            throw;
        }

        camera.PublisherExited += (_, code) =>
        {
            string output = camera.LastFfmpegOutput;
            Log($"FFmpeg for '{path}' exited with code {code}. Stream is down."
                + (output.Length > 0 ? Environment.NewLine + output : string.Empty));
        };
        camera.SkipRanges = video.Skips;

        var entry = new CameraEntry(path, video, camera);
        _entries.Add(entry);
        var panel = new CameraPanel(this, entry);
        _panels[entry] = panel;
        _cameras.Controls.Add(panel);
        LayoutPanels();
        _cameras.ScrollControlIntoView(panel);

        Log($"Added camera {camera.Url} playing {video.Name}.");
        _streamPath.Text = NextStreamPath();
        UpdateFileItem(video.Path);

        // A camera is live as soon as it is added. If FFmpeg cannot start, it stays (stopped)
        // and the error is shown.
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

    /// <summary>The video for a file: the one already in use, or opened now (settings, skips, motion, previews).</summary>
    private VideoFile OpenVideo(string file)
    {
        string full = Path.GetFullPath(file);
        if (_videos.TryGetValue(full, out var existing))
            return existing;

        var video = new VideoFile(RequireFfmpeg(), full);
        video.Message += (_, message) => Log(message);
        video.Changed += OnVideoChanged;
        video.Load(_options);
        _videos[full] = video;
        return video;
    }

    /// <summary>A video's skips changed (edited, scanned, new settings): every camera playing it follows.</summary>
    private void OnVideoChanged(object? sender, EventArgs e)
    {
        if (sender is not VideoFile video)
            return;

        foreach (var entry in CamerasOf(video))
            entry.Camera.SkipRanges = video.Skips;

        UpdateFileItem(video.Path);
    }

    private IEnumerable<CameraEntry> CamerasOf(VideoFile video) => _entries.Where(e => ReferenceEquals(e.Video, video)).ToList();

    private void ReleaseVideoIfUnused(VideoFile video)
    {
        if (_entries.Any(e => ReferenceEquals(e.Video, video)))
            return;

        _videos.Remove(video.Path);
        video.Changed -= OnVideoChanged;
        video.Dispose();
    }

    private string CheckStreamPath(string text)
    {
        string path = text.Trim().Trim('/');

        if (path.Length == 0)
            throw new InvalidOperationException("Enter a stream path, for example Streaming/Channels/101.");

        if (path.Any(c => char.IsWhiteSpace(c) || c is '?' or '#'))
            throw new InvalidOperationException("The stream path cannot contain spaces, '?' or '#'.");

        if (_entries.Any(e => e.StreamPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A camera with path '{path}' already exists.");

        return path;
    }

    /// <summary>The next free Hikvision-style path: Streaming/Channels/101, 201, 301 and so on.</summary>
    private string NextStreamPath()
    {
        for (int channel = 1; channel < 1000; channel++)
        {
            string candidate = $"Streaming/Channels/{channel}01";
            if (!_entries.Any(e => e.StreamPath.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return "Streaming/Channels/101";
    }

    public void StartCamera(CameraEntry entry)
    {
        entry.Camera.Start();
        Log($"Started {entry.Camera.Url}");
    }

    public void StopCamera(CameraEntry entry)
    {
        entry.Camera.Stop();
        Log($"Stopped {entry.Camera.Url}");
    }

    public void RemoveCamera(CameraEntry entry)
    {
        RemoveCameraQuietly(entry);
        Log($"Removed {entry.StreamPath}");

        // Offer the freed path again, unless the user typed a path of their own.
        if (System.Text.RegularExpressions.Regex.IsMatch(_streamPath.Text.Trim(), @"^Streaming/Channels/\d+01$"))
            _streamPath.Text = NextStreamPath();
        LayoutPanels();
        UpdateButtons();
    }

    private void RemoveCameraQuietly(CameraEntry entry)
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

        _entries.Remove(entry);
        if (_panels.TryGetValue(entry, out var panel))
        {
            _panels.Remove(entry);
            _cameras.Controls.Remove(panel);
            panel.Dispose();
        }

        ReleaseVideoIfUnused(entry.Video);
        UpdateFileItem(entry.Video.Path);
    }

    public void ScanMotion(CameraEntry entry)
    {
        var video = entry.Video;

        if (video.IsScanning)
            throw new InvalidOperationException($"{video.Name} is being scanned already.");

        if (video.Skips.Count > 0 && MessageBox.Show(this,
                $"Replace the {video.Skips.Count} skip(s) of {video.Name} with the result of a new motion scan?",
                "Scan motion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        video.StartScan(automatic: false);
    }

    public async Task OpenInVlcAsync(CameraEntry entry)
    {
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

    public async Task SimulateOutageAsync(CameraEntry entry, int seconds)
    {
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
    }

    /// <summary>Lays the camera panels out one under the other, as wide as the area.</summary>
    private void LayoutPanels()
    {
        _camerasHint.Visible = _panels.Count == 0;
        if (_panels.Count == 0)
            return;

        // Always leave room for the vertical scrollbar, so the panels never need a horizontal one
        // and do not jump when the vertical one appears.
        int width = _cameras.Width - SystemInformation.VerticalScrollBarWidth - 6;
        if (width < 200)
            return;

        foreach (var panel in _panels.Values)
        {
            if (panel.Width != width)
                panel.Width = width;

            int height = panel.HeightFor(width);
            if (panel.Height != height)
                panel.Height = height;
        }
    }

    // ===================================================================== settings

    /// <summary>
    /// The main settings: defaults for videos without their own, and the server. Cameras of
    /// videos that follow the defaults restart if their stream changes; new motion settings pick
    /// their skips again from the saved scans.
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

        var old = _options;
        _options = edited;

        if (!OptionsStore.Save(_options))
            Log("WARNING: the settings could not be saved to disk, so they will be lost when the app closes.");

        var before = VideoSettings.From(old);
        var after = VideoSettings.From(_options);
        bool publishingChanged = old.PublishTransport != _options.PublishTransport || old.FfmpegLogLevel != _options.FfmpegLogLevel;
        int own = _videos.Values.Count(v => v.HasOwnSettings);

        Log($"Defaults saved: {Describe(_options)}."
            + (own > 0 ? $" {own} video(s) with their own settings keep them." : string.Empty));

        var restart = new List<CameraEntry>();
        foreach (var video in _videos.Values.ToList())
        {
            if (video.HasOwnSettings)
            {
                if (publishingChanged)
                    restart.AddRange(CamerasOf(video));
                continue;
            }

            ReapplyMotion(video, before, after);

            if (!before.SameStream(after) || publishingChanged)
                restart.AddRange(CamerasOf(video));
        }

        await RestartAsync(restart);

        if (_server is not null && _serverOptions is not null && !_serverOptions.ServerSettingsEqual(_options))
            Log("The server settings (transports, timeouts, logins, log level) changed. " +
                "Stop and start the server for them to take effect.");
    }

    /// <summary>One video's own settings, saved next to it. "Use defaults" in the dialog goes back to the defaults.</summary>
    public async Task EditVideoSettingsAsync(CameraEntry entry)
    {
        var video = entry.Video;
        var before = video.Effective(_options);

        var current = _options.Clone();
        before.ApplyTo(current);

        StreamOptions result;
        using (var dialog = new OptionsDialog(current, video.Name, _options))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            result = dialog.Result;
        }

        var after = VideoSettings.From(result);
        var defaults = VideoSettings.From(_options);
        bool sameAsDefaults = after.SameStream(defaults) && after.SameMotion(defaults);

        video.SetOwnSettings(sameAsDefaults ? null : after);
        ReapplyMotion(video, before, after);

        if (!before.SameStream(after))
            await RestartAsync(CamerasOf(video));
    }

    /// <summary>
    /// New motion settings: picks the skips again from the saved motion data, at once and
    /// without rescanning. Skips edited by hand are only replaced if the user agrees.
    /// </summary>
    private void ReapplyMotion(VideoFile video, VideoSettings before, VideoSettings after)
    {
        if (before.SameMotion(after) || video.Motion is null)
            return;

        if (video.SkipsMatchScan(before)
            || MessageBox.Show(this,
                $"{video.Name} has skips you changed by hand. Pick its skips again from the saved motion data, "
                + "with the new motion settings? Your changes would be replaced.",
                "New motion settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            video.ApplyMotionSettings(after);
        }
        else
        {
            Log($"{video.Name}: skips kept as you edited them. Scan motion picks them from a fresh scan.");
        }
    }

    private async Task RestartAsync(IEnumerable<CameraEntry> entries)
    {
        var list = entries.Distinct().ToList();
        if (list.Count == 0)
            return;

        await RunBusyAsync(async () =>
        {
            foreach (var entry in list)
                await ApplySettingsToCameraAsync(entry);
        });
    }

    /// <summary>
    /// Restarts a camera with its current settings, waits until the new stream plays, and
    /// reopens its VLC window. Viewers are disconnected by the restart, just as with a real
    /// camera that changes resolution, and VLC does not reconnect by itself.
    /// </summary>
    private async Task ApplySettingsToCameraAsync(CameraEntry entry)
    {
        try
        {
            bool hadVlc = VlcLauncher.IsOpen(entry.Vlc);
            bool wasOnline = entry.Camera.IsOnline;

            entry.Camera.ApplyOptions(CameraOptions(entry.Video));

            if (!wasOnline)
            {
                Log($"'{entry.StreamPath}': uses the new settings when started.");
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

    /// <summary>
    /// The options for a camera: the defaults, with the video's own settings over them, and the
    /// logins the running server was started with (changed logins are refused until it restarts).
    /// </summary>
    private StreamOptions CameraOptions(VideoFile video)
    {
        var options = _options.Clone();
        video.Effective(_options).ApplyTo(options);

        if (_serverOptions is not null)
        {
            options.ViewerUser = _serverOptions.ViewerUser;
            options.ViewerPassword = _serverOptions.ViewerPassword;
            options.PublisherUser = _serverOptions.PublisherUser;
            options.PublisherPassword = _serverOptions.PublisherPassword;
        }

        return options;
    }

    // ===================================================================== helpers

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

        _port.Enabled = !serverUp && !_busy;
        _optionsButton.Enabled = !_busy;
        _startServer.Enabled = !serverUp && !_busy && _mediaMtxExe is not null;
        _stopServer.Enabled = serverUp && !_busy;
        _addCamera.Enabled = serverUp && !_busy && _files.SelectedItems.Count > 0;
        _streamPath.Enabled = !_busy;

        _statusText.Text = serverUp
            ? $"MediaMTX running on port {_server!.Port}  |  cameras: {_entries.Count}"
            : _mediaMtxExe is null ? "MediaMTX not found" : "Server stopped";
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

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");

    public void Log(string message)
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
