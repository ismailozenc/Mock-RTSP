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

        public override string ToString() =>
            $"{(Camera.IsOnline ? "[ON]  " : "[off] ")}{StreamPath}    ({Path.GetFileName(SourceFile)})";
    }

    // Found in the "mediamtx" and "ffmpeg" folders next to the program when the form loads. Null if missing.
    private string? _mediaMtxExe;
    private string? _ffmpegExe;

    private StreamOptions _options = new();         // Saved in options.json. Loaded in OnLoad.
    private StreamOptions? _serverOptions;          // What the running server was started with.
    private readonly List<CameraEntry> _entries = new();
    private MediaMtxServer? _server;
    private bool _busy;

    public MainForm()
    {
        InitializeComponent();
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
            Safe(StopServer, "Shutdown");

        base.OnFormClosing(e);
    }

    // ---- Event handlers (wired in MainForm.Designer.cs) ----

    private void OnBrowseVideoClick(object? sender, EventArgs e) => Safe(BrowseVideo, "Browse for video");

    private async void OnStartServerClick(object? sender, EventArgs e) =>
        await SafeAsync(StartServerAsync, "Start server");

    private void OnStopServerClick(object? sender, EventArgs e) => Safe(StopServer, "Stop server");

    private void OnOptionsClick(object? sender, EventArgs e) => Safe(EditOptions, "Stream options");

    private void OnAddCameraClick(object? sender, EventArgs e) => Safe(AddCamera, "Add camera");

    private void OnOpenVlcClick(object? sender, EventArgs e) => Safe(OpenInVlc, "Open in VLC");

    private void OnQualityClick(object? sender, EventArgs e) => Safe(EditCameraQuality, "Camera quality");

    private void OnStartCameraClick(object? sender, EventArgs e) => Safe(StartSelectedCamera, "Start camera");

    private void OnStopCameraClick(object? sender, EventArgs e) => Safe(StopSelectedCamera, "Stop camera");

    private void OnRemoveCameraClick(object? sender, EventArgs e) => Safe(RemoveSelectedCamera, "Remove camera");

    private async void OnOutageClick(object? sender, EventArgs e) =>
        await SafeAsync(SimulateOutageAsync, "Simulate outage");

    private void OnCamerasSelectedIndexChanged(object? sender, EventArgs e) => UpdateButtons();

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

        RecentFiles.Add(source);
        RefreshRecentFiles();

        Log($"Added camera {camera.Url}");
        UpdateButtons();
    }

    private void OpenInVlc()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");

        if (!entry.Camera.IsOnline)
            throw new InvalidOperationException("Start the camera first, then open it in VLC.");

        string vlc = VlcLauncher.Find()
            ?? throw new FileNotFoundException(
                "VLC was not found. Install VLC, or put vlc.exe in a 'vlc' folder next to MockRtsp.exe.");

        VlcLauncher.Open(vlc, entry.Camera.ViewerUrl);
        Log($"Opened {entry.Camera.Url} in VLC.");
    }

    private void EditCameraQuality()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");

        using var dialog = new OptionsDialog(entry.Camera.Options, cameraOnly: true);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        bool wasOnline = entry.Camera.IsOnline;
        entry.Camera.ApplyOptions(dialog.Result);

        Log(wasOnline
            ? $"Quality saved for '{entry.StreamPath}'. Camera restarted with the new settings."
            : $"Quality saved for '{entry.StreamPath}'. It applies when the camera starts.");
        UpdateButtons();
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

        Log($"Removed {entry.StreamPath}");
        UpdateButtons();
    }

    private async Task SimulateOutageAsync()
    {
        var entry = SelectedEntry ?? throw new InvalidOperationException("Select a camera first.");
        int seconds = (int)_outageSeconds.Value;

        Log($"Simulating outage on '{entry.StreamPath}' for {seconds} s...");

        await RunBusyAsync(() => entry.Camera.SimulateOutageAsync(TimeSpan.FromSeconds(seconds)));

        Log($"Outage on '{entry.StreamPath}' ended. Camera restarted.");
        UpdateButtons();
    }

    private void EditOptions()
    {
        using var dialog = new OptionsDialog(_options);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _options = dialog.Result;

        if (!OptionsStore.Save(_options))
            Log("WARNING: the options could not be saved to disk, so they will be lost when the app closes.");

        // FFmpeg settings apply to every camera now. Running cameras restart.
        int applied = 0;
        foreach (var entry in _entries.ToList())
        {
            try
            {
                entry.Camera.ApplyOptions(CameraOptions());
                applied++;
            }
            catch (Exception ex)
            {
                ErrorReporter.Report(ex, $"Applying options to {entry.StreamPath}");
                Log($"ERROR: could not apply the options to '{entry.StreamPath}': {ex.Message}");
            }
        }

        Log(applied > 0
            ? $"Stream options saved and applied to {applied} camera(s). Running cameras were restarted."
            : "Stream options saved. They apply to cameras you add from now on.");

        if (_server is not null && _serverOptions is not null && !_serverOptions.ServerSettingsEqual(_options))
            Log("The MediaMTX settings (transports, timeouts, logins, log level) changed. " +
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
        _quality.Enabled = !_busy && selected is not null;
        _openVlc.Enabled = !_busy && selectedOnline;
        _outage.Enabled = !_busy && selected is not null;

        _statusText.Text = serverUp
            ? $"MediaMTX running on port {_server!.Port}  |  cameras: {_entries.Count}"
            : _mediaMtxExe is null ? "MediaMTX not found" : "Server stopped";
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
