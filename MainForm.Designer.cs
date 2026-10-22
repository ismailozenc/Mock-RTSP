#nullable enable

namespace MockRtsp;

// Layout:
//   server row     port, start/stop server, main settings
//   folder row     the video folder (with recent folders), browse, refresh
//   left           the playable videos in that folder; double-click to add one as a camera
//   right          one panel per camera, each with its own controls and timeline
//   bottom         the log
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
    private FlowLayoutPanel _folderRow = null!;
    private Label _lblFolder = null!;
    private ComboBox _folder = null!;
    private Button _browseFolder = null!;
    private Button _refreshFolder = null!;
    private Label _folderInfo = null!;
    private SplitContainer _split = null!;
    private SplitContainer _content = null!;
    private TableLayoutPanel _filesLayout = null!;
    private Label _lblFiles = null!;
    private ListView _files = null!;
    private ColumnHeader _colName = null!;
    private ColumnHeader _colLength = null!;
    private ColumnHeader _colPicture = null!;
    private ColumnHeader _colSkips = null!;
    private ColumnHeader _colCamera = null!;
    private FlowLayoutPanel _addRow = null!;
    private Label _lblStreamPath = null!;
    private TextBox _streamPath = null!;
    private Button _addCamera = null!;
    private TableLayoutPanel _camerasLayout = null!;
    private Label _lblCameras = null!;
    private FlowLayoutPanel _cameras = null!;
    private Label _camerasHint = null!;
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
        _folderRow = new FlowLayoutPanel();
        _lblFolder = new Label();
        _folder = new ComboBox();
        _browseFolder = new Button();
        _refreshFolder = new Button();
        _folderInfo = new Label();
        _split = new SplitContainer();
        _content = new SplitContainer();
        _filesLayout = new TableLayoutPanel();
        _lblFiles = new Label();
        _files = new ListView();
        _colName = new ColumnHeader();
        _colLength = new ColumnHeader();
        _colPicture = new ColumnHeader();
        _colSkips = new ColumnHeader();
        _colCamera = new ColumnHeader();
        _addRow = new FlowLayoutPanel();
        _lblStreamPath = new Label();
        _streamPath = new TextBox();
        _addCamera = new Button();
        _camerasLayout = new TableLayoutPanel();
        _lblCameras = new Label();
        _cameras = new FlowLayoutPanel();
        _camerasHint = new Label();
        _log = new TextBox();
        _status = new StatusStrip();
        _statusText = new ToolStripStatusLabel();
        _timer = new System.Windows.Forms.Timer(components);

        SuspendLayout();
        _root.SuspendLayout();
        _serverRow.SuspendLayout();
        _folderRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_split).BeginInit();
        _split.Panel1.SuspendLayout();
        _split.Panel2.SuspendLayout();
        _split.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_content).BeginInit();
        _content.Panel1.SuspendLayout();
        _content.Panel2.SuspendLayout();
        _content.SuspendLayout();
        _filesLayout.SuspendLayout();
        _addRow.SuspendLayout();
        _camerasLayout.SuspendLayout();
        _cameras.SuspendLayout();

        // ================= Server row =================
        _lblPort.AutoSize = true;
        _lblPort.Margin = new Padding(6, 8, 2, 0);
        _lblPort.Text = "Port:";
        _port.Margin = new Padding(0, 5, 6, 0);
        _port.Minimum = 1025;
        _port.Maximum = 64534;
        _port.Value = 8554;
        _port.Width = 80;
        _startServer.AutoSize = true;
        _startServer.Text = "Start server";
        _stopServer.AutoSize = true;
        _stopServer.Text = "Stop server";
        _optionsButton.AutoSize = true;
        _optionsButton.Margin = new Padding(14, 3, 3, 3);
        _optionsButton.Text = "Settings (defaults and server)...";

        _serverRow.AutoSize = true;
        _serverRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _serverRow.Margin = new Padding(0, 0, 0, 2);
        _serverRow.WrapContents = true;
        _serverRow.Controls.AddRange(new Control[] { _lblPort, _port, _startServer, _stopServer, _optionsButton });

        // ================= Folder row =================
        _lblFolder.AutoSize = true;
        _lblFolder.Margin = new Padding(6, 8, 2, 0);
        _lblFolder.Text = "Video folder:";
        _folder.DropDownStyle = ComboBoxStyle.DropDown;   // type a path, or pick a recent folder
        _folder.DropDownWidth = 700;
        _folder.Margin = new Padding(0, 4, 3, 0);
        _folder.Width = 560;
        _browseFolder.AutoSize = true;
        _browseFolder.Text = "Browse...";
        _refreshFolder.AutoSize = true;
        _refreshFolder.Text = "Refresh";
        _folderInfo.AutoSize = true;
        _folderInfo.ForeColor = SystemColors.GrayText;
        _folderInfo.Margin = new Padding(10, 8, 3, 0);
        _folderInfo.Text = "Choose the folder with your camera videos.";

        _folderRow.AutoSize = true;
        _folderRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _folderRow.Margin = new Padding(0, 0, 0, 4);
        _folderRow.WrapContents = true;
        _folderRow.Controls.AddRange(new Control[] { _lblFolder, _folder, _browseFolder, _refreshFolder, _folderInfo });

        // ================= Left: the videos in the folder =================
        _lblFiles.AutoSize = true;
        _lblFiles.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _lblFiles.Margin = new Padding(0, 2, 0, 4);
        _lblFiles.Text = "Videos (double-click to add as a camera; select several for a playlist)";

        _colName.Text = "Video";
        _colName.Width = 140;
        _colLength.Text = "Length";
        _colLength.Width = 56;
        _colPicture.Text = "Picture";
        _colPicture.Width = 104;
        _colSkips.Text = "Skips";
        _colSkips.Width = 140;
        _colCamera.Text = "Camera";
        _colCamera.Width = 88;
        _files.Columns.AddRange(new ColumnHeader[] { _colName, _colLength, _colPicture, _colSkips, _colCamera });
        _files.Dock = DockStyle.Fill;
        _files.FullRowSelect = true;
        _files.HideSelection = false;
        _files.MultiSelect = true;
        _files.View = View.Details;

        _lblStreamPath.AutoSize = true;
        _lblStreamPath.Margin = new Padding(0, 8, 2, 0);
        _lblStreamPath.Text = "Stream path:";
        _streamPath.Margin = new Padding(0, 4, 3, 0);
        _streamPath.Text = "Streaming/Channels/101";
        _streamPath.Width = 200;
        _addCamera.AutoSize = true;
        _addCamera.Text = "Add camera";

        _addRow.AutoSize = true;
        _addRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _addRow.Dock = DockStyle.Fill;
        _addRow.Margin = new Padding(0, 4, 0, 0);
        _addRow.WrapContents = true;
        _addRow.Controls.AddRange(new Control[] { _lblStreamPath, _streamPath, _addCamera });

        _filesLayout.ColumnCount = 1;
        _filesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _filesLayout.Dock = DockStyle.Fill;
        _filesLayout.RowCount = 3;
        _filesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _filesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _filesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _filesLayout.Controls.Add(_lblFiles, 0, 0);
        _filesLayout.Controls.Add(_files, 0, 1);
        _filesLayout.Controls.Add(_addRow, 0, 2);

        // ================= Right: one panel per camera =================
        _lblCameras.AutoSize = true;
        _lblCameras.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _lblCameras.Margin = new Padding(0, 2, 0, 4);
        _lblCameras.Text = "Cameras";

        _camerasHint.AutoSize = true;
        _camerasHint.ForeColor = SystemColors.GrayText;
        _camerasHint.Margin = new Padding(4, 8, 4, 4);
        _camerasHint.Text = "Start the server, then double-click a video on the left to add it as a camera.";

        _cameras.AutoScroll = true;
        _cameras.Dock = DockStyle.Fill;
        _cameras.FlowDirection = FlowDirection.TopDown;
        _cameras.WrapContents = false;
        _cameras.Controls.Add(_camerasHint);

        _camerasLayout.ColumnCount = 1;
        _camerasLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _camerasLayout.Dock = DockStyle.Fill;
        _camerasLayout.RowCount = 2;
        _camerasLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _camerasLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _camerasLayout.Controls.Add(_lblCameras, 0, 0);
        _camerasLayout.Controls.Add(_cameras, 0, 1);

        // ================= Videos | cameras, and the log below =================
        _content.Dock = DockStyle.Fill;
        _content.FixedPanel = FixedPanel.Panel1;          // extra width goes to the cameras
        _content.Orientation = Orientation.Vertical;
        _content.Panel1.Controls.Add(_filesLayout);
        _content.Panel1.Padding = new Padding(0, 0, 4, 0);
        _content.Panel2.Controls.Add(_camerasLayout);
        _content.Panel2.Padding = new Padding(4, 0, 0, 0);

        _log.Dock = DockStyle.Fill;
        _log.Font = new Font("Consolas", 9F);
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;

        _split.Dock = DockStyle.Fill;
        _split.FixedPanel = FixedPanel.Panel2;            // extra height goes to the cameras
        _split.Orientation = Orientation.Horizontal;
        _split.Panel1.Controls.Add(_content);
        _split.Panel2.Controls.Add(_log);

        // ================= Status bar =================
        _statusText.Text = "Server stopped";
        _status.Items.Add(_statusText);

        // ================= Root =================
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Dock = DockStyle.Fill;
        _root.Padding = new Padding(8);
        _root.RowCount = 3;
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.Controls.Add(_serverRow, 0, 0);
        _root.Controls.Add(_folderRow, 0, 1);
        _root.Controls.Add(_split, 0, 2);

        // ================= Events =================
        _startServer.Click += new EventHandler(OnStartServerClick);
        _stopServer.Click += new EventHandler(OnStopServerClick);
        _optionsButton.Click += new EventHandler(OnOptionsClick);
        _folder.SelectionChangeCommitted += new EventHandler(OnFolderPicked);
        _folder.KeyDown += new KeyEventHandler(OnFolderKeyDown);
        _browseFolder.Click += new EventHandler(OnBrowseFolderClick);
        _refreshFolder.Click += new EventHandler(OnRefreshFolderClick);
        _files.DoubleClick += new EventHandler(OnAddCameraClick);
        _files.SelectedIndexChanged += new EventHandler(OnFilesSelectionChanged);
        _addCamera.Click += new EventHandler(OnAddCameraClick);
        _cameras.Resize += new EventHandler(OnCamerasResize);

        // Timer: refreshes the camera panels and picks up finished scans, four times a second.
        _timer.Interval = 250;
        _timer.Tick += new EventHandler(OnTimerTick);

        // ================= Form =================
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1400, 860);
        MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "MockRtsp - mock RTSP cameras";
        // Docking runs from the last added control to the first, so the edge-docked status bar
        // must be added after the Fill panel. Otherwise the panel runs underneath it.
        Controls.Add(_root);
        Controls.Add(_status);

        _cameras.ResumeLayout(false);
        _cameras.PerformLayout();
        _camerasLayout.ResumeLayout(false);
        _camerasLayout.PerformLayout();
        _addRow.ResumeLayout(false);
        _addRow.PerformLayout();
        _filesLayout.ResumeLayout(false);
        _filesLayout.PerformLayout();
        _content.Panel1.ResumeLayout(false);
        _content.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_content).EndInit();
        _content.ResumeLayout(false);
        _split.Panel1.ResumeLayout(false);
        _split.Panel2.ResumeLayout(false);
        _split.Panel2.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_split).EndInit();
        _split.ResumeLayout(false);
        _folderRow.ResumeLayout(false);
        _folderRow.PerformLayout();
        _serverRow.ResumeLayout(false);
        _serverRow.PerformLayout();
        _root.ResumeLayout(false);
        _root.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
