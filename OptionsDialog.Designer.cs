#nullable enable

namespace MockRtsp;

// Layout: each tab holds one two-column grid (label | control) with bold section headings.
// No nested auto-sizing containers: a GroupBox that auto-sizes around a grid docked Fill
// grows without limit, which pushed most of this dialog off the screen.
public sealed partial class OptionsDialog
{
    private System.ComponentModel.IContainer? components = null;

    private TabControl _tabs = null!;
    private TabPage _tabFfmpeg = null!;
    private TabPage _tabMtx = null!;
    private TableLayoutPanel _bottom = null!;
    private Button _reset = null!;
    private Button _cancel = null!;
    private Button _ok = null!;
    private ToolTip _tips = null!;

    // FFmpeg tab
    private TableLayoutPanel _ffGrid = null!;
    private Label _ffIntro = null!;
    private Label _hdrTiming = null!;
    private CheckBox _loop = null!;
    private Label _hdrMotion = null!;
    private CheckBox _autoScan = null!;
    private Label _lblMotionThreshold = null!;
    private NumericUpDown _motionThreshold = null!;
    private Label _lblMinStill = null!;
    private NumericUpDown _minStill = null!;
    private Label _lblMotionPadding = null!;
    private NumericUpDown _motionPadding = null!;
    private Label _hdrEncode = null!;
    private Label _lblBitrate = null!;
    private NumericUpDown _bitrate = null!;
    private Label _lblKeyframe = null!;
    private NumericUpDown _keyframe = null!;
    private Label _lblSize = null!;
    private FlowLayoutPanel _sizeRow = null!;
    private NumericUpDown _width = null!;
    private Label _sizeX = null!;
    private NumericUpDown _height = null!;
    private Label _lblFrameRate = null!;
    private NumericUpDown _frameRate = null!;
    private CheckBox _noBFrames = null!;
    private Label _hdrPublish = null!;
    private Label _lblPublishTransport = null!;
    private ComboBox _publishTransport = null!;
    private Label _lblFfmpegLog = null!;
    private ComboBox _ffmpegLog = null!;

    // MediaMTX tab
    private TableLayoutPanel _mtxGrid = null!;
    private Label _mtxIntro = null!;
    private Label _hdrNetwork = null!;
    private Label _lblTransports = null!;
    private ComboBox _transports = null!;
    private Label _lblReadTimeout = null!;
    private NumericUpDown _readTimeout = null!;
    private Label _lblWriteTimeout = null!;
    private NumericUpDown _writeTimeout = null!;
    private Label _hdrLogging = null!;
    private Label _lblMtxLog = null!;
    private ComboBox _mtxLog = null!;
    private Label _hdrViewer = null!;
    private Label _lblViewerUser = null!;
    private TextBox _viewerUser = null!;
    private Label _lblViewerPassword = null!;
    private TextBox _viewerPassword = null!;
    private Label _hdrPublisher = null!;
    private Label _lblPublisherUser = null!;
    private TextBox _publisherUser = null!;
    private Label _lblPublisherPassword = null!;
    private TextBox _publisherPassword = null!;

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
        _tips = new ToolTip(components);

        _tabs = new TabControl();
        _tabFfmpeg = new TabPage();
        _tabMtx = new TabPage();
        _bottom = new TableLayoutPanel();
        _reset = new Button();
        _cancel = new Button();
        _ok = new Button();

        _ffGrid = new TableLayoutPanel();
        _ffIntro = new Label();
        _hdrTiming = new Label();
        _loop = new CheckBox();
        _hdrMotion = new Label();
        _autoScan = new CheckBox();
        _lblMotionThreshold = new Label();
        _motionThreshold = new NumericUpDown();
        _lblMinStill = new Label();
        _minStill = new NumericUpDown();
        _lblMotionPadding = new Label();
        _motionPadding = new NumericUpDown();
        _hdrEncode = new Label();
        _lblBitrate = new Label();
        _bitrate = new NumericUpDown();
        _lblKeyframe = new Label();
        _keyframe = new NumericUpDown();
        _lblSize = new Label();
        _sizeRow = new FlowLayoutPanel();
        _width = new NumericUpDown();
        _sizeX = new Label();
        _height = new NumericUpDown();
        _lblFrameRate = new Label();
        _frameRate = new NumericUpDown();
        _noBFrames = new CheckBox();
        _hdrPublish = new Label();
        _lblPublishTransport = new Label();
        _publishTransport = new ComboBox();
        _lblFfmpegLog = new Label();
        _ffmpegLog = new ComboBox();

        _mtxGrid = new TableLayoutPanel();
        _mtxIntro = new Label();
        _hdrNetwork = new Label();
        _lblTransports = new Label();
        _transports = new ComboBox();
        _lblReadTimeout = new Label();
        _readTimeout = new NumericUpDown();
        _lblWriteTimeout = new Label();
        _writeTimeout = new NumericUpDown();
        _hdrLogging = new Label();
        _lblMtxLog = new Label();
        _mtxLog = new ComboBox();
        _hdrViewer = new Label();
        _lblViewerUser = new Label();
        _viewerUser = new TextBox();
        _lblViewerPassword = new Label();
        _viewerPassword = new TextBox();
        _hdrPublisher = new Label();
        _lblPublisherUser = new Label();
        _publisherUser = new TextBox();
        _lblPublisherPassword = new Label();
        _publisherPassword = new TextBox();

        SuspendLayout();

        // ================= Bottom bar: Reset on the left, Cancel and OK on the right =================
        _bottom.AutoSize = true;
        _bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _bottom.ColumnCount = 4;
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.Dock = DockStyle.Bottom;
        _bottom.Padding = new Padding(12, 8, 12, 10);
        _bottom.RowCount = 1;
        _bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _bottom.Controls.Add(_reset, 0, 0);
        _bottom.Controls.Add(_cancel, 2, 0);
        _bottom.Controls.Add(_ok, 3, 0);

        _reset.Margin = new Padding(0);
        _reset.Size = new Size(120, 30);
        _reset.Text = "Reset defaults";
        _reset.Click += new EventHandler(OnResetClick);

        _cancel.DialogResult = DialogResult.Cancel;
        _cancel.Margin = new Padding(6, 0, 0, 0);
        _cancel.Size = new Size(96, 30);
        _cancel.Text = "Cancel";

        _ok.Margin = new Padding(6, 0, 0, 0);
        _ok.Size = new Size(96, 30);
        _ok.Text = "OK";
        _ok.Click += new EventHandler(OnOkClick);

        // ================= FFmpeg tab =================
        _tabFfmpeg.AutoScroll = true;
        _tabFfmpeg.Padding = new Padding(10);
        _tabFfmpeg.Text = "Defaults for videos";
        _tabFfmpeg.UseVisualStyleBackColor = true;
        _tabFfmpeg.Controls.Add(_ffGrid);

        _ffGrid.AutoSize = true;
        _ffGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _ffGrid.ColumnCount = 2;
        _ffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _ffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _ffGrid.Dock = DockStyle.Top;
        _ffGrid.RowCount = 17;
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.Controls.Add(_ffIntro, 0, 0);
        _ffGrid.Controls.Add(_hdrTiming, 0, 1);
        _ffGrid.Controls.Add(_loop, 0, 2);
        _ffGrid.Controls.Add(_hdrMotion, 0, 3);
        _ffGrid.Controls.Add(_autoScan, 0, 4);
        _ffGrid.Controls.Add(_lblMotionThreshold, 0, 5);
        _ffGrid.Controls.Add(_motionThreshold, 1, 5);
        _ffGrid.Controls.Add(_lblMinStill, 0, 6);
        _ffGrid.Controls.Add(_minStill, 1, 6);
        _ffGrid.Controls.Add(_lblMotionPadding, 0, 7);
        _ffGrid.Controls.Add(_motionPadding, 1, 7);
        _ffGrid.Controls.Add(_hdrEncode, 0, 8);
        _ffGrid.Controls.Add(_lblSize, 0, 9);
        _ffGrid.Controls.Add(_sizeRow, 1, 9);
        _ffGrid.Controls.Add(_lblFrameRate, 0, 10);
        _ffGrid.Controls.Add(_frameRate, 1, 10);
        _ffGrid.Controls.Add(_lblBitrate, 0, 11);
        _ffGrid.Controls.Add(_bitrate, 1, 11);
        _ffGrid.Controls.Add(_lblKeyframe, 0, 12);
        _ffGrid.Controls.Add(_keyframe, 1, 12);
        _ffGrid.Controls.Add(_noBFrames, 0, 13);
        _ffGrid.Controls.Add(_hdrPublish, 0, 14);
        _ffGrid.Controls.Add(_lblPublishTransport, 0, 15);
        _ffGrid.Controls.Add(_publishTransport, 1, 15);
        _ffGrid.Controls.Add(_lblFfmpegLog, 0, 16);
        _ffGrid.Controls.Add(_ffmpegLog, 1, 16);
        _ffGrid.SetColumnSpan(_ffIntro, 2);
        _ffGrid.SetColumnSpan(_hdrTiming, 2);
        _ffGrid.SetColumnSpan(_loop, 2);
        _ffGrid.SetColumnSpan(_hdrMotion, 2);
        _ffGrid.SetColumnSpan(_autoScan, 2);
        _ffGrid.SetColumnSpan(_hdrEncode, 2);
        _ffGrid.SetColumnSpan(_noBFrames, 2);
        _ffGrid.SetColumnSpan(_hdrPublish, 2);

        _ffIntro.AutoSize = true;
        _ffIntro.ForeColor = SystemColors.GrayText;
        _ffIntro.Margin = new Padding(0, 0, 0, 4);
        _ffIntro.MaximumSize = new Size(590, 0);
        _ffIntro.Text = "Used by every video that has no settings of its own (give a video its own with Video settings on its camera). Cameras restart with new settings, keeping position, pause and speed. New motion settings pick skips again from saved scans, without scanning again.";

        _hdrTiming.AutoSize = true;
        _hdrTiming.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _hdrTiming.Margin = new Padding(0, 10, 0, 2);
        _hdrTiming.Text = "Playback";

        _loop.AutoSize = true;
        _loop.Checked = true;
        _loop.Margin = new Padding(12, 2, 0, 2);
        _loop.Text = "Loop the video when it ends (otherwise hold the last frame)";

        _hdrMotion.AutoSize = true;
        _hdrMotion.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _hdrMotion.Margin = new Padding(0, 12, 0, 2);
        _hdrMotion.Text = "Motion scan (skip parts where nothing moves)";

        _autoScan.AutoSize = true;
        _autoScan.Checked = true;
        _autoScan.Margin = new Padding(12, 2, 0, 2);
        _autoScan.Text = "Scan a video for motion the first time it is used (videos with saved data are not rescanned)";

        _lblMotionThreshold.AutoSize = true;
        _lblMotionThreshold.Margin = new Padding(12, 6, 10, 0);
        _lblMotionThreshold.Text = "Movement needed (% of the picture):";

        _motionThreshold.DecimalPlaces = 1;
        _motionThreshold.Increment = 0.1M;
        _motionThreshold.Margin = new Padding(0, 3, 0, 3);
        _motionThreshold.Maximum = 25;
        _motionThreshold.Minimum = 0.1M;
        _motionThreshold.Value = 0.3M;
        _motionThreshold.Width = 110;

        _lblMinStill.AutoSize = true;
        _lblMinStill.Margin = new Padding(12, 6, 10, 0);
        _lblMinStill.Text = "Skip still parts longer than (seconds):";

        _minStill.DecimalPlaces = 1;
        _minStill.Increment = 0.5M;
        _minStill.Margin = new Padding(0, 3, 0, 3);
        _minStill.Maximum = 3600;
        _minStill.Minimum = 1;
        _minStill.Value = 3;
        _minStill.Width = 110;

        _lblMotionPadding.AutoSize = true;
        _lblMotionPadding.Margin = new Padding(12, 6, 10, 0);
        _lblMotionPadding.Text = "Keep before and after movement (seconds):";

        _motionPadding.DecimalPlaces = 1;
        _motionPadding.Increment = 0.5M;
        _motionPadding.Margin = new Padding(0, 3, 0, 3);
        _motionPadding.Maximum = 60;
        _motionPadding.Value = 1;
        _motionPadding.Width = 110;



        _hdrEncode.AutoSize = true;
        _hdrEncode.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _hdrEncode.Margin = new Padding(0, 12, 0, 2);
        _hdrEncode.Text = "Resolution and quality";



        _lblBitrate.AutoSize = true;
        _lblBitrate.Margin = new Padding(12, 6, 10, 0);
        _lblBitrate.Text = "Bitrate (kbps, 0 = default):";

        _bitrate.Increment = 100;
        _bitrate.Margin = new Padding(0, 3, 0, 3);
        _bitrate.Maximum = 100000;
        _bitrate.Width = 110;

        _lblKeyframe.AutoSize = true;
        _lblKeyframe.Margin = new Padding(12, 6, 10, 0);
        _lblKeyframe.Text = "Keyframe interval (frames, 0 = every second):";

        _keyframe.Margin = new Padding(0, 3, 0, 3);
        _keyframe.Maximum = 1000;
        _keyframe.Width = 110;

        _lblSize.AutoSize = true;
        _lblSize.Margin = new Padding(12, 6, 10, 0);
        _lblSize.Text = "Resolution (width x height, 0 = keep):";

        _sizeRow.AutoSize = true;
        _sizeRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sizeRow.Margin = new Padding(0);
        _sizeRow.WrapContents = false;
        _sizeRow.Controls.Add(_width);
        _sizeRow.Controls.Add(_sizeX);
        _sizeRow.Controls.Add(_height);

        _width.Increment = 16;
        _width.Margin = new Padding(0, 3, 0, 3);
        _width.Maximum = 7680;
        _width.Width = 80;

        _sizeX.AutoSize = true;
        _sizeX.Margin = new Padding(6, 6, 6, 0);
        _sizeX.Text = "x";

        _height.Increment = 16;
        _height.Margin = new Padding(0, 3, 0, 3);
        _height.Maximum = 4320;
        _height.Width = 80;

        _lblFrameRate.AutoSize = true;
        _lblFrameRate.Margin = new Padding(12, 6, 10, 0);
        _lblFrameRate.Text = "Frame rate (fps, 0 = keep):";

        _frameRate.Margin = new Padding(0, 3, 0, 3);
        _frameRate.Maximum = 240;
        _frameRate.Width = 110;

        _noBFrames.AutoSize = true;
        _noBFrames.Margin = new Padding(12, 4, 0, 2);
        _noBFrames.Text = "Disable B-frames (lower latency)";

        _hdrPublish.AutoSize = true;
        _hdrPublish.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _hdrPublish.Margin = new Padding(0, 12, 0, 2);
        _hdrPublish.Text = "Publishing";

        _lblPublishTransport.AutoSize = true;
        _lblPublishTransport.Margin = new Padding(12, 6, 10, 0);
        _lblPublishTransport.Text = "Transport to MediaMTX:";

        _publishTransport.DropDownStyle = ComboBoxStyle.DropDownList;
        _publishTransport.Margin = new Padding(0, 3, 0, 3);
        _publishTransport.Width = 140;

        _lblFfmpegLog.AutoSize = true;
        _lblFfmpegLog.Margin = new Padding(12, 6, 10, 0);
        _lblFfmpegLog.Text = "FFmpeg log level:";

        _ffmpegLog.DropDownStyle = ComboBoxStyle.DropDownList;
        _ffmpegLog.Margin = new Padding(0, 3, 0, 3);
        _ffmpegLog.Width = 140;

        // ================= MediaMTX tab =================
        _tabMtx.AutoScroll = true;
        _tabMtx.Padding = new Padding(10);
        _tabMtx.Text = "Server";
        _tabMtx.UseVisualStyleBackColor = true;
        _tabMtx.Controls.Add(_mtxGrid);

        _mtxGrid.AutoSize = true;
        _mtxGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _mtxGrid.ColumnCount = 2;
        _mtxGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _mtxGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mtxGrid.Dock = DockStyle.Top;
        _mtxGrid.RowCount = 13;
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.Controls.Add(_mtxIntro, 0, 0);
        _mtxGrid.Controls.Add(_hdrNetwork, 0, 1);
        _mtxGrid.Controls.Add(_lblTransports, 0, 2);
        _mtxGrid.Controls.Add(_transports, 1, 2);
        _mtxGrid.Controls.Add(_lblReadTimeout, 0, 3);
        _mtxGrid.Controls.Add(_readTimeout, 1, 3);
        _mtxGrid.Controls.Add(_lblWriteTimeout, 0, 4);
        _mtxGrid.Controls.Add(_writeTimeout, 1, 4);
        _mtxGrid.Controls.Add(_hdrLogging, 0, 5);
        _mtxGrid.Controls.Add(_lblMtxLog, 0, 6);
        _mtxGrid.Controls.Add(_mtxLog, 1, 6);
        _mtxGrid.Controls.Add(_hdrViewer, 0, 7);
        _mtxGrid.Controls.Add(_lblViewerUser, 0, 8);
        _mtxGrid.Controls.Add(_viewerUser, 1, 8);
        _mtxGrid.Controls.Add(_lblViewerPassword, 0, 9);
        _mtxGrid.Controls.Add(_viewerPassword, 1, 9);
        _mtxGrid.Controls.Add(_hdrPublisher, 0, 10);
        _mtxGrid.Controls.Add(_lblPublisherUser, 0, 11);
        _mtxGrid.Controls.Add(_publisherUser, 1, 11);
        _mtxGrid.Controls.Add(_lblPublisherPassword, 0, 12);
        _mtxGrid.Controls.Add(_publisherPassword, 1, 12);
        _mtxGrid.SetColumnSpan(_mtxIntro, 2);
        _mtxGrid.SetColumnSpan(_hdrNetwork, 2);
        _mtxGrid.SetColumnSpan(_hdrLogging, 2);
        _mtxGrid.SetColumnSpan(_hdrViewer, 2);
        _mtxGrid.SetColumnSpan(_hdrPublisher, 2);

        _mtxIntro.AutoSize = true;
        _mtxIntro.ForeColor = SystemColors.GrayText;
        _mtxIntro.Margin = new Padding(0, 0, 0, 4);
        _mtxIntro.MaximumSize = new Size(590, 0);
        _mtxIntro.Text = "The RTSP server. Changes here apply after you stop and start the server.";

        _hdrNetwork.AutoSize = true;
        _hdrNetwork.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _hdrNetwork.Margin = new Padding(0, 10, 0, 2);
        _hdrNetwork.Text = "Network and timeouts";

        _lblTransports.AutoSize = true;
        _lblTransports.Margin = new Padding(12, 6, 10, 0);
        _lblTransports.Text = "RTSP transports:";

        _transports.DropDownStyle = ComboBoxStyle.DropDownList;
        _transports.Margin = new Padding(0, 3, 0, 3);
        _transports.Width = 140;

        _lblReadTimeout.AutoSize = true;
        _lblReadTimeout.Margin = new Padding(12, 6, 10, 0);
        _lblReadTimeout.Text = "Read timeout (seconds):";

        _readTimeout.Margin = new Padding(0, 3, 0, 3);
        _readTimeout.Maximum = 300;
        _readTimeout.Minimum = 1;
        _readTimeout.Value = 10;
        _readTimeout.Width = 110;

        _lblWriteTimeout.AutoSize = true;
        _lblWriteTimeout.Margin = new Padding(12, 6, 10, 0);
        _lblWriteTimeout.Text = "Write timeout (seconds):";

        _writeTimeout.Margin = new Padding(0, 3, 0, 3);
        _writeTimeout.Maximum = 300;
        _writeTimeout.Minimum = 1;
        _writeTimeout.Value = 10;
        _writeTimeout.Width = 110;

        _hdrLogging.AutoSize = true;
        _hdrLogging.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _hdrLogging.Margin = new Padding(0, 12, 0, 2);
        _hdrLogging.Text = "Logging";

        _lblMtxLog.AutoSize = true;
        _lblMtxLog.Margin = new Padding(12, 6, 10, 0);
        _lblMtxLog.Text = "MediaMTX log level:";

        _mtxLog.DropDownStyle = ComboBoxStyle.DropDownList;
        _mtxLog.Margin = new Padding(0, 3, 0, 3);
        _mtxLog.Width = 140;

        _hdrViewer.AutoSize = true;
        _hdrViewer.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _hdrViewer.Margin = new Padding(0, 12, 0, 2);
        _hdrViewer.Text = "Viewer login (optional, required to watch)";

        _lblViewerUser.AutoSize = true;
        _lblViewerUser.Margin = new Padding(12, 6, 10, 0);
        _lblViewerUser.Text = "Username (blank = none):";

        _viewerUser.Margin = new Padding(0, 3, 0, 3);
        _viewerUser.Width = 200;

        _lblViewerPassword.AutoSize = true;
        _lblViewerPassword.Margin = new Padding(12, 6, 10, 0);
        _lblViewerPassword.Text = "Password:";

        _viewerPassword.Margin = new Padding(0, 3, 0, 3);
        _viewerPassword.UseSystemPasswordChar = true;
        _viewerPassword.Width = 200;

        _hdrPublisher.AutoSize = true;
        _hdrPublisher.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _hdrPublisher.Margin = new Padding(0, 12, 0, 2);
        _hdrPublisher.Text = "Publisher login (optional, required to send)";

        _lblPublisherUser.AutoSize = true;
        _lblPublisherUser.Margin = new Padding(12, 6, 10, 0);
        _lblPublisherUser.Text = "Username (blank = none):";

        _publisherUser.Margin = new Padding(0, 3, 0, 3);
        _publisherUser.Width = 200;

        _lblPublisherPassword.AutoSize = true;
        _lblPublisherPassword.Margin = new Padding(12, 6, 10, 0);
        _lblPublisherPassword.Text = "Password:";

        _publisherPassword.Margin = new Padding(0, 3, 0, 3);
        _publisherPassword.UseSystemPasswordChar = true;
        _publisherPassword.Width = 200;

        // ================= Tabs =================
        _tabs.Dock = DockStyle.Fill;
        _tabs.Padding = new Point(12, 4);
        _tabs.Controls.Add(_tabFfmpeg);
        _tabs.Controls.Add(_tabMtx);

        // ================= Tooltips =================
        _tips.SetToolTip(_loop, "Replay the video from the start when it reaches the end. Off stops the camera after one pass.");
        _tips.SetToolTip(_motionThreshold, "Lower finds smaller movement (a person far away); higher ignores small changes such as leaves or rain.");
        _tips.SetToolTip(_minStill, "Still parts shorter than this keep playing, so short pauses in the action are not cut.");
        _tips.SetToolTip(_motionPadding, "Time kept before movement starts and after it ends, so a car is seen arriving and leaving.");
        _tips.SetToolTip(_keyframe, "A new viewer shows nothing until the next keyframe. 0 sends one every second, so playback starts within about a second.");
        _tips.SetToolTip(_transports, "Which transports MediaMTX accepts from viewers. TCP is the most reliable.");
        _tips.SetToolTip(_viewerUser, "Set both a username and a password to require a login for viewers.");
        _tips.SetToolTip(_publisherUser, "Set both a username and a password to require a login for the publisher.");

        // ================= Form =================
        AcceptButton = _ok;
        CancelButton = _cancel;
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(640, 600);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Settings";

        // Docking runs from the last added control to the first, so the edge-docked bar
        // must be added after the Fill control. Otherwise the tabs take the whole window.
        Controls.Add(_tabs);
        Controls.Add(_bottom);

        ResumeLayout(false);
        PerformLayout();
    }
}
