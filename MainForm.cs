namespace MockRtsp;

public sealed class MainForm : Form
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

    // Found at startup in the "mediamtx" and "ffmpeg" folders next to the program. Null if missing.
    private readonly string? _mediaMtxExe;
    private readonly string? _ffmpegExe;

    // Server row
    private readonly NumericUpDown _port = new() { Minimum = 1025, Maximum = 64534, Value = 8554, Width = 80 };
    private readonly Button _startServer = new() { Text = "Start server", AutoSize = true };
    private readonly Button _stopServer = new() { Text = "Stop server", AutoSize = true };

    // Add camera row
    private readonly TextBox _streamPath = new() { Text = "Streaming/Channels/101", Width = 220 };
    private readonly TextBox _sourceFile = new() { Width = 360 };
    private readonly Button _browseVideo = new() { Text = "Browse...", AutoSize = true };
    private readonly Button _addCamera = new() { Text = "Add camera", AutoSize = true };

    // Camera list and actions
    private readonly ListBox _cameras = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Button _startCamera = new() { Text = "Start", AutoSize = true };
    private readonly Button _stopCamera = new() { Text = "Stop", AutoSize = true };
    private readonly Button _removeCamera = new() { Text = "Remove", AutoSize = true };
    private readonly NumericUpDown _outageSeconds = new() { Minimum = 1, Maximum = 3600, Value = 5, Width = 70 };
    private readonly Button _outage = new() { Text = "Simulate outage", AutoSize = true };

    // Log and status
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9F)
    };
    private readonly ToolStripStatusLabel _statusText = new() { Text = "Server stopped" };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

    private readonly List<CameraEntry> _entries = new();
    private MediaMtxServer? _server;
    private bool _busy;

    public MainForm()
    {
        Text = "MockRtsp - mock RTSP camera";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1000, 720);
        MinimumSize = new Size(900, 600);

        // Auto setup: look for the bundled tools once, at startup.
        _mediaMtxExe = Toolchain.Find(Toolchain.MediaMtxFolder, "mediamtx.exe");
        _ffmpegExe = Toolchain.Find(Toolchain.FfmpegFolder, "ffmpeg.exe");

        BuildLayout();
        WireEvents();

        _timer.Start();
        UpdateButtons();

        Log(_mediaMtxExe is not null
            ? $"MediaMTX: {_mediaMtxExe}"
            : $"MediaMTX not found. Put mediamtx.exe in the '{Toolchain.MediaMtxFolder}' folder next to MockRtsp.exe.");
        Log(_ffmpegExe is not null
            ? $"FFmpeg: {_ffmpegExe}"
            : $"FFmpeg not found. Put ffmpeg.exe in the '{Toolchain.FfmpegFolder}' folder next to MockRtsp.exe.");
        Log("Start the MediaMTX server, then add cameras.");
    }

    private CameraEntry? SelectedEntry => _cameras.SelectedItem as CameraEntry;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(8)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // server row
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // add camera row
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));   // camera list
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // camera actions
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));   // log

        root.Controls.Add(Row(
            Caption("Port:"), _port, _startServer, _stopServer), 0, 0);

        root.Controls.Add(Row(
            Caption("Stream path:"), _streamPath,
            Caption("Video file:"), _sourceFile, _browseVideo, _addCamera), 0, 1);

        root.Controls.Add(_cameras, 0, 2);

        root.Controls.Add(Row(
            _startCamera, _stopCamera, _removeCamera,
            Caption("Outage (s):"), _outageSeconds, _outage), 0, 3);

        root.Controls.Add(_log, 0, 4);

        // StatusStrip docks to the bottom. Add it before the Fill panel so the layout is correct.
        var status = new StatusStrip();
        status.Items.Add(_statusText);
        Controls.Add(status);
        Controls.Add(root);
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        panel.Controls.AddRange(controls);
        return panel;
    }

    private static Label Caption(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(6, 6, 2, 0) };

    private void WireEvents()
    {
        _browseVideo.Click += (_, _) => Safe(BrowseVideo, "Browse for video");
        _startServer.Click += async (_, _) => await SafeAsync(StartServerAsync, "Start server");
        _stopServer.Click += (_, _) => Safe(StopServer, "Stop server");
        _addCamera.Click += (_, _) => Safe(AddCamera, "Add camera");
        _startCamera.Click += (_, _) => Safe(StartSelectedCamera, "Start camera");
        _stopCamera.Click += (_, _) => Safe(StopSelectedCamera, "Stop camera");
        _removeCamera.Click += (_, _) => Safe(RemoveSelectedCamera, "Remove camera");
        _outage.Click += async (_, _) => await SafeAsync(SimulateOutageAsync, "Simulate outage");
        _cameras.SelectedIndexChanged += (_, _) => UpdateButtons();
        _timer.Tick += (_, _) => OnTimerTick();
        FormClosing += (_, _) => Safe(StopServer, "Shutdown");
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

    private void OnTimerTick()
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

        await RunBusyAsync(async () =>
        {
            _server = await MediaMtxServer.StartAsync(port, exe, log: Log);
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

        var camera = new MockCamera(_server, path, source, RequireFfmpeg());
        camera.PublisherExited += (_, code) =>
            Log($"FFmpeg for '{path}' exited with code {code}. Stream is down.");

        var entry = new CameraEntry(path, source, camera);
        _entries.Add(entry);
        _cameras.Items.Add(entry);
        _cameras.SelectedItem = entry;

        Log($"Added camera {camera.Url}");
        UpdateButtons();
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
        _startServer.Enabled = !serverUp && !_busy && _mediaMtxExe is not null;
        _stopServer.Enabled = serverUp && !_busy;

        _streamPath.Enabled = serverUp && !_busy;
        _sourceFile.Enabled = serverUp && !_busy;
        _browseVideo.Enabled = serverUp && !_busy;
        _addCamera.Enabled = serverUp && !_busy;

        _startCamera.Enabled = !_busy && selected is not null && !selectedOnline;
        _stopCamera.Enabled = !_busy && selected is not null && selectedOnline;
        _removeCamera.Enabled = !_busy && selected is not null;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}
