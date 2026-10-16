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
    private TextBox _sourceFile = null!;
    private Button _browseVideo = null!;
    private Button _addCamera = null!;
    private ListBox _cameras = null!;
    private FlowLayoutPanel _actionsRow = null!;
    private Button _startCamera = null!;
    private Button _stopCamera = null!;
    private Button _removeCamera = null!;
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
        _sourceFile = new TextBox();
        _browseVideo = new Button();
        _addCamera = new Button();
        _cameras = new ListBox();
        _actionsRow = new FlowLayoutPanel();
        _startCamera = new Button();
        _stopCamera = new Button();
        _removeCamera = new Button();
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
        _optionsButton.Text = "Stream options...";

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
        _cameras.IntegralHeight = false;

        // Camera actions row
        _startCamera.AutoSize = true;
        _startCamera.Text = "Start";
        _stopCamera.AutoSize = true;
        _stopCamera.Text = "Stop";
        _removeCamera.AutoSize = true;
        _removeCamera.Text = "Remove";
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
        _actionsRow.Controls.AddRange(new Control[] { _startCamera, _stopCamera, _removeCamera, _lblOutage, _outageSeconds, _outage });

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
        _root.RowCount = 5;
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        _root.Controls.Add(_serverRow, 0, 0);
        _root.Controls.Add(_cameraRow, 0, 1);
        _root.Controls.Add(_cameras, 0, 2);
        _root.Controls.Add(_actionsRow, 0, 3);
        _root.Controls.Add(_log, 0, 4);

        // Events
        _startServer.Click += new EventHandler(OnStartServerClick);
        _stopServer.Click += new EventHandler(OnStopServerClick);
        _optionsButton.Click += new EventHandler(OnOptionsClick);
        _browseVideo.Click += new EventHandler(OnBrowseVideoClick);
        _addCamera.Click += new EventHandler(OnAddCameraClick);
        _startCamera.Click += new EventHandler(OnStartCameraClick);
        _stopCamera.Click += new EventHandler(OnStopCameraClick);
        _removeCamera.Click += new EventHandler(OnRemoveCameraClick);
        _outage.Click += new EventHandler(OnOutageClick);
        _cameras.SelectedIndexChanged += new EventHandler(OnCamerasSelectedIndexChanged);

        // Timer (refreshes camera status once a second)
        _timer.Interval = 1000;
        _timer.Tick += new EventHandler(OnTimerTick);

        // Form
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 700);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "MockRtsp - mock RTSP camera";
        Controls.Add(_status);      // status bar first so the Fill panel docks correctly
        Controls.Add(_root);

        _serverRow.ResumeLayout(false);
        _cameraRow.ResumeLayout(false);
        _actionsRow.ResumeLayout(false);
        _root.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
