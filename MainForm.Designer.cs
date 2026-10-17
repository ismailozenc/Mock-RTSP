#nullable enable

namespace MockRtsp;

public sealed partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    private TableLayoutPanel _root = null!;
    private FlowLayoutPanel _serverRow = null!;
    private Label _lblPort = null!;
    private NumericUpDown _port = null!;
    private Button _startServer = null!;
    private Button _stopServer = null!;
    private Button _optionsButton = null!;
    private FlowLayoutPanel _cameraRow = null!;
    private Label _lblStreamPath = null!;
    private TextBox _streamPath = null!;
    private Label _lblVideo = null!;
    private ComboBox _sourceFile = null!;
    private Button _browseVideo = null!;
    private Button _addCamera = null!;
    private ListBox _cameras = null!;
    private FlowLayoutPanel _playerRow = null!;
    private Button _back10 = null!;
    private Button _playPause = null!;
    private Button _forward10 = null!;
    private TrackBar _seekBar = null!;
    private Label _timeLabel = null!;
    private Label _lblSpeed = null!;
    private ComboBox _speed = null!;
    private FlowLayoutPanel _actionsRow = null!;
    private Button _startCamera = null!;
    private Button _stopCamera = null!;
    private Button _removeCamera = null!;
    private Button _openVlc = null!;
    private Label _lblOutage = null!;
    private NumericUpDown _outageSeconds = null!;
    private Button _outage = null!;
    private TextBox _log = null!;
    private StatusStrip _status = null!;
    private ToolStripStatusLabel _statusText = null!;
    private System.Windows.Forms.Timer _timer = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        _root = new TableLayoutPanel();
        _serverRow = new FlowLayoutPanel();
        _lblPort = new Label();
        _port = new NumericUpDown();
        _startServer = new Button();
        _stopServer = new Button();
        _optionsButton = new Button();
        _cameraRow = new FlowLayoutPanel();
        _lblStreamPath = new Label();
        _streamPath = new TextBox();
        _lblVideo = new Label();
        _sourceFile = new ComboBox();
        _browseVideo = new Button();
        _addCamera = new Button();
        _cameras = new ListBox();
        _playerRow = new FlowLayoutPanel();
        _back10 = new Button();
        _playPause = new Button();
        _forward10 = new Button();
        _seekBar = new TrackBar();
        _timeLabel = new Label();
        _lblSpeed = new Label();
        _speed = new ComboBox();
        _actionsRow = new FlowLayoutPanel();
        _startCamera = new Button();
        _stopCamera = new Button();
        _removeCamera = new Button();
        _openVlc = new Button();
        _lblOutage = new Label();
        _outageSeconds = new NumericUpDown();
        _outage = new Button();
        _log = new TextBox();
        _status = new StatusStrip();
        _statusText = new ToolStripStatusLabel();
        _timer = new System.Windows.Forms.Timer(components);

        SuspendLayout();
        _root.SuspendLayout();
        _serverRow.SuspendLayout();
        _cameraRow.SuspendLayout();
        _playerRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_seekBar).BeginInit();
        _actionsRow.SuspendLayout();

        // _lblPort
        _lblPort.AutoSize = true;
        _lblPort.Margin = new Padding(6, 6, 2, 0);
        _lblPort.Text = "Port:";

        // _port (set Maximum before Value so the value is in range)
        _port.Minimum = 1025;
        _port.Maximum = 64534;
        _port.Value = 8554;
        _port.Width = 80;

        // Server buttons
        _startServer.AutoSize = true;
        _startServer.Text = "Start server";
        _stopServer.AutoSize = true;
        _stopServer.Text = "Stop server";
        _optionsButton.AutoSize = true;
        _optionsButton.Text = "Settings...";

        // _serverRow
        _serverRow.AutoSize = true;
        _serverRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _serverRow.Margin = new Padding(0, 0, 0, 4);
        _serverRow.WrapContents = true;
        _serverRow.Controls.AddRange(new Control[] { _lblPort, _port, _startServer, _stopServer, _optionsButton });

        // Add camera row
        _lblStreamPath.AutoSize = true;
        _lblStreamPath.Margin = new Padding(6, 6, 2, 0);
        _lblStreamPath.Text = "Stream path:";
        _streamPath.Text = "Streaming/Channels/101";
        _streamPath.Width = 220;
        _lblVideo.AutoSize = true;
        _lblVideo.Margin = new Padding(6, 6, 2, 0);
        _lblVideo.Text = "Video file:";
        _sourceFile.DropDownStyle = ComboBoxStyle.DropDown;   // typed paths allowed; recent files in the list
        _sourceFile.DropDownWidth = 600;
        _sourceFile.Width = 360;
        _browseVideo.AutoSize = true;
        _browseVideo.Text = "Browse...";
        _addCamera.AutoSize = true;
        _addCamera.Text = "Add camera";

        // _cameraRow
        _cameraRow.AutoSize = true;
        _cameraRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _cameraRow.Margin = new Padding(0, 0, 0, 4);
        _cameraRow.WrapContents = true;
        _cameraRow.Controls.AddRange(new Control[] { _lblStreamPath, _streamPath, _lblVideo, _sourceFile, _browseVideo, _addCamera });

        // Camera list
        _cameras.Dock = DockStyle.Fill;
        _cameras.DrawMode = DrawMode.OwnerDrawFixed;    // drawn live, so [ON]/[off] stays current
        _cameras.IntegralHeight = false;
        _cameras.ItemHeight = 22;

        // Player row: controls the selected camera. Viewers stay connected through all of these.
        _back10.Size = new Size(70, 28);
        _back10.Text = "« 10 s";
        _playPause.Size = new Size(80, 28);
        _playPause.Text = "Pause";
        _forward10.Size = new Size(70, 28);
        _forward10.Text = "10 s »";
        _seekBar.AutoSize = false;
        _seekBar.Margin = new Padding(6, 2, 6, 0);
        _seekBar.Maximum = 100;
        _seekBar.Size = new Size(380, 30);
        _seekBar.TickStyle = TickStyle.None;
        _timeLabel.AutoSize = true;
        _timeLabel.Font = new Font("Consolas", 9F);
        _timeLabel.Margin = new Padding(0, 7, 12, 0);
        _timeLabel.Text = "--:-- / --:--";
        _lblSpeed.AutoSize = true;
        _lblSpeed.Margin = new Padding(0, 7, 2, 0);
        _lblSpeed.Text = "Speed:";
        _speed.DropDownStyle = ComboBoxStyle.DropDownList;
        _speed.Items.AddRange(new object[] { "0.25x", "0.5x", "1x", "2x", "4x" });
        _speed.Margin = new Padding(0, 4, 0, 0);
        _speed.Width = 70;

        _playerRow.AutoSize = true;
        _playerRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _playerRow.Margin = new Padding(0, 4, 0, 2);
        _playerRow.WrapContents = true;
        _playerRow.Controls.AddRange(new Control[] { _back10, _playPause, _forward10, _seekBar, _timeLabel, _lblSpeed, _speed });

        // Camera actions row
        _startCamera.AutoSize = true;
        _startCamera.Text = "Start";
        _stopCamera.AutoSize = true;
        _stopCamera.Text = "Stop";
        _removeCamera.AutoSize = true;
        _removeCamera.Text = "Remove";
        _openVlc.AutoSize = true;
        _openVlc.Text = "Open in VLC";
        _lblOutage.AutoSize = true;
        _lblOutage.Margin = new Padding(6, 6, 2, 0);
        _lblOutage.Text = "Outage (s):";
        _outageSeconds.Minimum = 1;
        _outageSeconds.Maximum = 3600;
        _outageSeconds.Value = 5;
        _outageSeconds.Width = 70;
        _outage.AutoSize = true;
        _outage.Text = "Simulate outage";

        _actionsRow.AutoSize = true;
        _actionsRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _actionsRow.Margin = new Padding(0, 0, 0, 4);
        _actionsRow.WrapContents = true;
        _actionsRow.Controls.AddRange(new Control[] { _startCamera, _stopCamera, _removeCamera, _openVlc, _lblOutage, _outageSeconds, _outage });

        // Log
        _log.Dock = DockStyle.Fill;
        _log.Font = new Font("Consolas", 9F);
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;

        // Status bar
        _statusText.Text = "Server stopped";
        _status.Items.Add(_statusText);

        // _root
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Dock = DockStyle.Fill;
        _root.Padding = new Padding(8);
        _root.RowCount = 6;
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        _root.Controls.Add(_serverRow, 0, 0);
        _root.Controls.Add(_cameraRow, 0, 1);
        _root.Controls.Add(_cameras, 0, 2);
        _root.Controls.Add(_playerRow, 0, 3);
        _root.Controls.Add(_actionsRow, 0, 4);
        _root.Controls.Add(_log, 0, 5);

        // Events
        _startServer.Click += new EventHandler(OnStartServerClick);
        _stopServer.Click += new EventHandler(OnStopServerClick);
        _optionsButton.Click += new EventHandler(OnOptionsClick);
        _browseVideo.Click += new EventHandler(OnBrowseVideoClick);
        _addCamera.Click += new EventHandler(OnAddCameraClick);
        _startCamera.Click += new EventHandler(OnStartCameraClick);
        _stopCamera.Click += new EventHandler(OnStopCameraClick);
        _removeCamera.Click += new EventHandler(OnRemoveCameraClick);
        _openVlc.Click += new EventHandler(OnOpenVlcClick);
        _outage.Click += new EventHandler(OnOutageClick);
        _cameras.SelectedIndexChanged += new EventHandler(OnCamerasSelectedIndexChanged);
        _cameras.DrawItem += new DrawItemEventHandler(OnCamerasDrawItem);
        _back10.Click += new EventHandler(OnBack10Click);
        _playPause.Click += new EventHandler(OnPlayPauseClick);
        _forward10.Click += new EventHandler(OnForward10Click);
        _seekBar.MouseDown += new MouseEventHandler(OnSeekBarMouseDown);
        _seekBar.MouseUp += new MouseEventHandler(OnSeekBarMouseUp);
        _seekBar.KeyUp += new KeyEventHandler(OnSeekBarKeyUp);
        _seekBar.Scroll += new EventHandler(OnSeekBarScroll);
        _speed.SelectedIndexChanged += new EventHandler(OnSpeedChanged);

        // Timer (refreshes camera status and the player position four times a second)
        _timer.Interval = 250;
        _timer.Tick += new EventHandler(OnTimerTick);

        // Form
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 700);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "MockRtsp - mock RTSP camera";
        // Docking runs from the last added control to the first, so the edge-docked status bar
        // must be added after the Fill panel. Otherwise the panel runs underneath it.
        Controls.Add(_root);
        Controls.Add(_status);

        _serverRow.ResumeLayout(false);
        _cameraRow.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_seekBar).EndInit();
        _playerRow.ResumeLayout(false);
        _actionsRow.ResumeLayout(false);
        _root.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
