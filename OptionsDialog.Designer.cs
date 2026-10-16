namespace MockRtsp;

public sealed partial class OptionsDialog
{
    private System.ComponentModel.IContainer? components = null;

    private Panel _bottom = null!;
    private Button _reset = null!;
    private Button _ok = null!;
    private Button _cancel = null!;
    private TabControl _tabs = null!;
    private TabPage _tabFfmpeg = null!;
    private TabPage _tabMtx = null!;
    private ToolTip _tips = null!;

    // FFmpeg tab
    private TableLayoutPanel _ffGrid = null!;
    private Label _ffIntro = null!;
    private GroupBox _grpTiming = null!;
    private TableLayoutPanel _timingGrid = null!;
    private CheckBox _loop = null!;
    private CheckBox _realTime = null!;
    private CheckBox _dropAudio = null!;
    private GroupBox _grpEncode = null!;
    private TableLayoutPanel _encodeGrid = null!;
    private CheckBox _reEncode = null!;
    private Label _reEncodeHint = null!;
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
    private GroupBox _grpPublish = null!;
    private TableLayoutPanel _publishGrid = null!;
    private Label _lblPublishTransport = null!;
    private ComboBox _publishTransport = null!;
    private Label _lblFfmpegLog = null!;
    private ComboBox _ffmpegLog = null!;

    // MediaMTX tab
    private TableLayoutPanel _mtxGrid = null!;
    private Label _mtxIntro = null!;
    private GroupBox _grpNetwork = null!;
    private TableLayoutPanel _netGrid = null!;
    private Label _lblTransports = null!;
    private ComboBox _transports = null!;
    private Label _lblReadTimeout = null!;
    private NumericUpDown _readTimeout = null!;
    private Label _lblWriteTimeout = null!;
    private NumericUpDown _writeTimeout = null!;
    private GroupBox _grpLogging = null!;
    private TableLayoutPanel _logGrid = null!;
    private Label _lblMtxLog = null!;
    private ComboBox _mtxLog = null!;
    private GroupBox _grpViewer = null!;
    private TableLayoutPanel _viewerGrid = null!;
    private Label _lblViewerUser = null!;
    private TextBox _viewerUser = null!;
    private Label _lblViewerPassword = null!;
    private TextBox _viewerPassword = null!;
    private GroupBox _grpPublisher = null!;
    private TableLayoutPanel _pubGrid = null!;
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

        _bottom = new Panel();
        _reset = new Button();
        _ok = new Button();
        _cancel = new Button();
        _tabs = new TabControl();
        _tabFfmpeg = new TabPage();
        _tabMtx = new TabPage();

        _ffGrid = new TableLayoutPanel();
        _ffIntro = new Label();
        _grpTiming = new GroupBox();
        _timingGrid = new TableLayoutPanel();
        _loop = new CheckBox();
        _realTime = new CheckBox();
        _dropAudio = new CheckBox();
        _grpEncode = new GroupBox();
        _encodeGrid = new TableLayoutPanel();
        _reEncode = new CheckBox();
        _reEncodeHint = new Label();
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
        _grpPublish = new GroupBox();
        _publishGrid = new TableLayoutPanel();
        _lblPublishTransport = new Label();
        _publishTransport = new ComboBox();
        _lblFfmpegLog = new Label();
        _ffmpegLog = new ComboBox();

        _mtxGrid = new TableLayoutPanel();
        _mtxIntro = new Label();
        _grpNetwork = new GroupBox();
        _netGrid = new TableLayoutPanel();
        _lblTransports = new Label();
        _transports = new ComboBox();
        _lblReadTimeout = new Label();
        _readTimeout = new NumericUpDown();
        _lblWriteTimeout = new Label();
        _writeTimeout = new NumericUpDown();
        _grpLogging = new GroupBox();
        _logGrid = new TableLayoutPanel();
        _lblMtxLog = new Label();
        _mtxLog = new ComboBox();
        _grpViewer = new GroupBox();
        _viewerGrid = new TableLayoutPanel();
        _lblViewerUser = new Label();
        _viewerUser = new TextBox();
        _lblViewerPassword = new Label();
        _viewerPassword = new TextBox();
        _grpPublisher = new GroupBox();
        _pubGrid = new TableLayoutPanel();
        _lblPublisherUser = new Label();
        _publisherUser = new TextBox();
        _lblPublisherPassword = new Label();
        _publisherPassword = new TextBox();

        SuspendLayout();

        // ---- Bottom bar ----
        _bottom.Dock = DockStyle.Bottom;
        _bottom.Height = 58;
        _bottom.Padding = new Padding(12, 10, 12, 10);

        _reset.Location = new Point(12, 14);
        _reset.Size = new Size(120, 30);
        _reset.Text = "Reset defaults";
        _reset.Click += new EventHandler(OnResetClick);

        _cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _cancel.DialogResult = DialogResult.Cancel;
        _cancel.Location = new Point(420, 14);
        _cancel.Size = new Size(96, 30);
        _cancel.Text = "Cancel";

        _ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _ok.Location = new Point(524, 14);
        _ok.Size = new Size(96, 30);
        _ok.Text = "OK";
        _ok.Click += new EventHandler(OnOkClick);

        _bottom.Controls.Add(_reset);
        _bottom.Controls.Add(_cancel);
        _bottom.Controls.Add(_ok);

        // ---- FFmpeg tab ----
        _tabFfmpeg.Padding = new Padding(10);
        _tabFfmpeg.Text = "FFmpeg (camera stream)";
        _tabFfmpeg.Controls.Add(_ffGrid);

        _ffIntro.AutoSize = true;
        _ffIntro.ForeColor = SystemColors.GrayText;
        _ffIntro.Margin = new Padding(4, 4, 4, 8);
        _ffIntro.MaximumSize = new Size(560, 0);
        _ffIntro.Text = "How each camera's video is sent. Changes apply to cameras added from now on.";

        _ffGrid.AutoSize = true;
        _ffGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _ffGrid.ColumnCount = 1;
        _ffGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _ffGrid.Dock = DockStyle.Fill;
        _ffGrid.RowCount = 4;
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _ffGrid.Controls.Add(_ffIntro, 0, 0);
        _ffGrid.Controls.Add(_grpTiming, 0, 1);
        _ffGrid.Controls.Add(_grpEncode, 0, 2);
        _ffGrid.Controls.Add(_grpPublish, 0, 3);

        // Timing group
        _grpTiming.AutoSize = true;
        _grpTiming.Dock = DockStyle.Fill;
        _grpTiming.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _grpTiming.Padding = new Padding(10, 6, 10, 8);
        _grpTiming.Text = "Looping and timing";
        _grpTiming.Controls.Add(_timingGrid);

        _timingGrid.AutoSize = true;
        _timingGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _timingGrid.ColumnCount = 1;
        _timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _timingGrid.Dock = DockStyle.Fill;
        _timingGrid.RowCount = 3;
        _timingGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _timingGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _timingGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _timingGrid.Controls.Add(_loop, 0, 0);
        _timingGrid.Controls.Add(_realTime, 0, 1);
        _timingGrid.Controls.Add(_dropAudio, 0, 2);

        _loop.AutoSize = true;
        _loop.Checked = true;
        _loop.Margin = new Padding(0, 3, 0, 3);
        _loop.Text = "Loop the video when it ends";

        _realTime.AutoSize = true;
        _realTime.Checked = true;
        _realTime.Margin = new Padding(0, 3, 0, 3);
        _realTime.Text = "Send at real-time speed (like a camera)";

        _dropAudio.AutoSize = true;
        _dropAudio.Checked = true;
        _dropAudio.Margin = new Padding(0, 3, 0, 3);
        _dropAudio.Text = "Drop audio";

        // Encoding group
        _grpEncode.AutoSize = true;
        _grpEncode.Dock = DockStyle.Fill;
        _grpEncode.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _grpEncode.Padding = new Padding(10, 6, 10, 8);
        _grpEncode.Text = "Video encoding";
        _grpEncode.Controls.Add(_encodeGrid);

        _encodeGrid.AutoSize = true;
        _encodeGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _encodeGrid.ColumnCount = 2;
        _encodeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _encodeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _encodeGrid.Dock = DockStyle.Fill;
        _encodeGrid.RowCount = 7;
        _encodeGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _encodeGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _encodeGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _encodeGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _encodeGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _encodeGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _encodeGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _encodeGrid.Controls.Add(_reEncode, 0, 0);
        _encodeGrid.Controls.Add(_reEncodeHint, 0, 1);
        _encodeGrid.Controls.Add(_lblBitrate, 0, 2);
        _encodeGrid.Controls.Add(_bitrate, 1, 2);
        _encodeGrid.Controls.Add(_lblKeyframe, 0, 3);
        _encodeGrid.Controls.Add(_keyframe, 1, 3);
        _encodeGrid.Controls.Add(_lblSize, 0, 4);
        _encodeGrid.Controls.Add(_sizeRow, 1, 4);
        _encodeGrid.Controls.Add(_lblFrameRate, 0, 5);
        _encodeGrid.Controls.Add(_frameRate, 1, 5);
        _encodeGrid.Controls.Add(_noBFrames, 0, 6);
        _encodeGrid.SetColumnSpan(_reEncode, 2);
        _encodeGrid.SetColumnSpan(_reEncodeHint, 2);
        _encodeGrid.SetColumnSpan(_noBFrames, 2);

        _reEncode.AutoSize = true;
        _reEncode.Margin = new Padding(0, 3, 0, 3);
        _reEncode.Text = "Re-encode to H.264 (libx264)";

        _reEncodeHint.AutoSize = true;
        _reEncodeHint.ForeColor = SystemColors.GrayText;
        _reEncodeHint.Margin = new Padding(20, 0, 0, 8);
        _reEncodeHint.MaximumSize = new Size(520, 0);
        _reEncodeHint.Text = "Off passes the camera video through unchanged. On enables the settings below.";

        _lblBitrate.AutoSize = true;
        _lblBitrate.Margin = new Padding(0, 6, 10, 0);
        _lblBitrate.Text = "Bitrate (kbps, 0 = default):";

        _bitrate.Maximum = 100000;
        _bitrate.Increment = 100;
        _bitrate.Width = 120;
        _bitrate.Margin = new Padding(0, 3, 0, 3);

        _lblKeyframe.AutoSize = true;
        _lblKeyframe.Margin = new Padding(0, 6, 10, 0);
        _lblKeyframe.Text = "Keyframe interval (frames, 0 = default):";

        _keyframe.Maximum = 1000;
        _keyframe.Width = 120;
        _keyframe.Margin = new Padding(0, 3, 0, 3);

        _lblSize.AutoSize = true;
        _lblSize.Margin = new Padding(0, 6, 10, 0);
        _lblSize.Text = "Size (width x height, 0 = keep):";

        _sizeRow.AutoSize = true;
        _sizeRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sizeRow.WrapContents = false;
        _sizeRow.Margin = new Padding(0);
        _sizeRow.Controls.Add(_width);
        _sizeRow.Controls.Add(_sizeX);
        _sizeRow.Controls.Add(_height);

        _width.Maximum = 7680;
        _width.Increment = 16;
        _width.Width = 90;
        _width.Margin = new Padding(0, 3, 0, 3);

        _sizeX.AutoSize = true;
        _sizeX.Margin = new Padding(6, 6, 6, 0);
        _sizeX.Text = "x";

        _height.Maximum = 4320;
        _height.Increment = 16;
        _height.Width = 90;
        _height.Margin = new Padding(0, 3, 0, 3);

        _lblFrameRate.AutoSize = true;
        _lblFrameRate.Margin = new Padding(0, 6, 10, 0);
        _lblFrameRate.Text = "Frame rate (0 = keep):";

        _frameRate.Maximum = 240;
        _frameRate.Width = 120;
        _frameRate.Margin = new Padding(0, 3, 0, 3);

        _noBFrames.AutoSize = true;
        _noBFrames.Margin = new Padding(0, 3, 0, 3);
        _noBFrames.Text = "Disable B-frames (lower latency)";

        // Publishing group
        _grpPublish.AutoSize = true;
        _grpPublish.Dock = DockStyle.Fill;
        _grpPublish.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _grpPublish.Padding = new Padding(10, 6, 10, 8);
        _grpPublish.Text = "Publishing";
        _grpPublish.Controls.Add(_publishGrid);

        _publishGrid.AutoSize = true;
        _publishGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _publishGrid.ColumnCount = 2;
        _publishGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _publishGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _publishGrid.Dock = DockStyle.Fill;
        _publishGrid.RowCount = 2;
        _publishGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _publishGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _publishGrid.Controls.Add(_lblPublishTransport, 0, 0);
        _publishGrid.Controls.Add(_publishTransport, 1, 0);
        _publishGrid.Controls.Add(_lblFfmpegLog, 0, 1);
        _publishGrid.Controls.Add(_ffmpegLog, 1, 1);

        _lblPublishTransport.AutoSize = true;
        _lblPublishTransport.Margin = new Padding(0, 6, 10, 0);
        _lblPublishTransport.Text = "Transport to MediaMTX:";

        _publishTransport.DropDownStyle = ComboBoxStyle.DropDownList;
        _publishTransport.Width = 160;
        _publishTransport.Margin = new Padding(0, 3, 0, 3);

        _lblFfmpegLog.AutoSize = true;
        _lblFfmpegLog.Margin = new Padding(0, 6, 10, 0);
        _lblFfmpegLog.Text = "FFmpeg log level:";

        _ffmpegLog.DropDownStyle = ComboBoxStyle.DropDownList;
        _ffmpegLog.Width = 160;
        _ffmpegLog.Margin = new Padding(0, 3, 0, 3);

        // ---- MediaMTX tab ----
        _tabMtx.Padding = new Padding(10);
        _tabMtx.Text = "MediaMTX (server)";
        _tabMtx.Controls.Add(_mtxGrid);

        _mtxGrid.AutoSize = true;
        _mtxGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _mtxGrid.ColumnCount = 1;
        _mtxGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _mtxGrid.Dock = DockStyle.Fill;
        _mtxGrid.RowCount = 5;
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mtxGrid.Controls.Add(_mtxIntro, 0, 0);
        _mtxGrid.Controls.Add(_grpNetwork, 0, 1);
        _mtxGrid.Controls.Add(_grpLogging, 0, 2);
        _mtxGrid.Controls.Add(_grpViewer, 0, 3);
        _mtxGrid.Controls.Add(_grpPublisher, 0, 4);

        _mtxIntro.AutoSize = true;
        _mtxIntro.ForeColor = SystemColors.GrayText;
        _mtxIntro.Margin = new Padding(4, 4, 4, 8);
        _mtxIntro.MaximumSize = new Size(560, 0);
        _mtxIntro.Text = "The RTSP server. Changes apply the next time the server starts.";

        // Network group
        _grpNetwork.AutoSize = true;
        _grpNetwork.Dock = DockStyle.Fill;
        _grpNetwork.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _grpNetwork.Padding = new Padding(10, 6, 10, 8);
        _grpNetwork.Text = "Network and timeouts";
        _grpNetwork.Controls.Add(_netGrid);

        _netGrid.AutoSize = true;
        _netGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _netGrid.ColumnCount = 2;
        _netGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _netGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _netGrid.Dock = DockStyle.Fill;
        _netGrid.RowCount = 3;
        _netGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _netGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _netGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _netGrid.Controls.Add(_lblTransports, 0, 0);
        _netGrid.Controls.Add(_transports, 1, 0);
        _netGrid.Controls.Add(_lblReadTimeout, 0, 1);
        _netGrid.Controls.Add(_readTimeout, 1, 1);
        _netGrid.Controls.Add(_lblWriteTimeout, 0, 2);
        _netGrid.Controls.Add(_writeTimeout, 1, 2);

        _lblTransports.AutoSize = true;
        _lblTransports.Margin = new Padding(0, 6, 10, 0);
        _lblTransports.Text = "RTSP transports:";

        _transports.DropDownStyle = ComboBoxStyle.DropDownList;
        _transports.Width = 160;
        _transports.Margin = new Padding(0, 3, 0, 3);

        _lblReadTimeout.AutoSize = true;
        _lblReadTimeout.Margin = new Padding(0, 6, 10, 0);
        _lblReadTimeout.Text = "Read timeout (seconds):";

        _readTimeout.Minimum = 1;
        _readTimeout.Maximum = 300;
        _readTimeout.Value = 10;
        _readTimeout.Width = 120;
        _readTimeout.Margin = new Padding(0, 3, 0, 3);

        _lblWriteTimeout.AutoSize = true;
        _lblWriteTimeout.Margin = new Padding(0, 6, 10, 0);
        _lblWriteTimeout.Text = "Write timeout (seconds):";

        _writeTimeout.Minimum = 1;
        _writeTimeout.Maximum = 300;
        _writeTimeout.Value = 10;
        _writeTimeout.Width = 120;
        _writeTimeout.Margin = new Padding(0, 3, 0, 3);

        // Logging group
        _grpLogging.AutoSize = true;
        _grpLogging.Dock = DockStyle.Fill;
        _grpLogging.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _grpLogging.Padding = new Padding(10, 6, 10, 8);
        _grpLogging.Text = "Logging";
        _grpLogging.Controls.Add(_logGrid);

        _logGrid.AutoSize = true;
        _logGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _logGrid.ColumnCount = 2;
        _logGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _logGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _logGrid.Dock = DockStyle.Fill;
        _logGrid.RowCount = 1;
        _logGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _logGrid.Controls.Add(_lblMtxLog, 0, 0);
        _logGrid.Controls.Add(_mtxLog, 1, 0);

        _lblMtxLog.AutoSize = true;
        _lblMtxLog.Margin = new Padding(0, 6, 10, 0);
        _lblMtxLog.Text = "MediaMTX log level:";

        _mtxLog.DropDownStyle = ComboBoxStyle.DropDownList;
        _mtxLog.Width = 160;
        _mtxLog.Margin = new Padding(0, 3, 0, 3);

        // Viewer login group
        _grpViewer.AutoSize = true;
        _grpViewer.Dock = DockStyle.Fill;
        _grpViewer.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _grpViewer.Padding = new Padding(10, 6, 10, 8);
        _grpViewer.Text = "Viewer login (optional, required to watch the stream)";
        _grpViewer.Controls.Add(_viewerGrid);

        _viewerGrid.AutoSize = true;
        _viewerGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _viewerGrid.ColumnCount = 2;
        _viewerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _viewerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _viewerGrid.Dock = DockStyle.Fill;
        _viewerGrid.RowCount = 2;
        _viewerGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _viewerGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _viewerGrid.Controls.Add(_lblViewerUser, 0, 0);
        _viewerGrid.Controls.Add(_viewerUser, 1, 0);
        _viewerGrid.Controls.Add(_lblViewerPassword, 0, 1);
        _viewerGrid.Controls.Add(_viewerPassword, 1, 1);

        _lblViewerUser.AutoSize = true;
        _lblViewerUser.Margin = new Padding(0, 6, 10, 0);
        _lblViewerUser.Text = "Username (blank = none):";

        _viewerUser.Width = 200;
        _viewerUser.Margin = new Padding(0, 3, 0, 3);

        _lblViewerPassword.AutoSize = true;
        _lblViewerPassword.Margin = new Padding(0, 6, 10, 0);
        _lblViewerPassword.Text = "Password:";

        _viewerPassword.Width = 200;
        _viewerPassword.UseSystemPasswordChar = true;
        _viewerPassword.Margin = new Padding(0, 3, 0, 3);

        // Publisher login group
        _grpPublisher.AutoSize = true;
        _grpPublisher.Dock = DockStyle.Fill;
        _grpPublisher.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _grpPublisher.Padding = new Padding(10, 6, 10, 8);
        _grpPublisher.Text = "Publisher login (optional, required to send the stream)";
        _grpPublisher.Controls.Add(_pubGrid);

        _pubGrid.AutoSize = true;
        _pubGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _pubGrid.ColumnCount = 2;
        _pubGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pubGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pubGrid.Dock = DockStyle.Fill;
        _pubGrid.RowCount = 2;
        _pubGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _pubGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _pubGrid.Controls.Add(_lblPublisherUser, 0, 0);
        _pubGrid.Controls.Add(_publisherUser, 1, 0);
        _pubGrid.Controls.Add(_lblPublisherPassword, 0, 1);
        _pubGrid.Controls.Add(_publisherPassword, 1, 1);

        _lblPublisherUser.AutoSize = true;
        _lblPublisherUser.Margin = new Padding(0, 6, 10, 0);
        _lblPublisherUser.Text = "Username (blank = none):";

        _publisherUser.Width = 200;
        _publisherUser.Margin = new Padding(0, 3, 0, 3);

        _lblPublisherPassword.AutoSize = true;
        _lblPublisherPassword.Margin = new Padding(0, 6, 10, 0);
        _lblPublisherPassword.Text = "Password:";

        _publisherPassword.Width = 200;
        _publisherPassword.UseSystemPasswordChar = true;
        _publisherPassword.Margin = new Padding(0, 3, 0, 3);

        // ---- Tabs ----
        _tabs.Dock = DockStyle.Fill;
        _tabs.Padding = new Point(14, 6);
        _tabs.Controls.Add(_tabFfmpeg);
        _tabs.Controls.Add(_tabMtx);

        // ---- Tooltips ----
        _tips.SetToolTip(_loop, "Replay the video from the start when it reaches the end. Off stops the camera after one pass.");
        _tips.SetToolTip(_realTime, "Sends frames at their normal rate. Turn off to send as fast as possible.");
        _tips.SetToolTip(_reEncode, "Re-encodes the video. Required for the bitrate, keyframe, size and frame rate settings.");
        _tips.SetToolTip(_keyframe, "Shorter intervals let a viewer start playback sooner, at a higher bitrate.");
        _tips.SetToolTip(_transports, "Which transports MediaMTX accepts from viewers. TCP is the most reliable.");
        _tips.SetToolTip(_viewerUser, "Set both a username and a password to require a login for viewers.");
        _tips.SetToolTip(_publisherUser, "Set both a username and a password to require a login for the publisher.");

        // ---- Form ----
        AcceptButton = _ok;
        CancelButton = _cancel;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(640, 600);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Stream options";
        Controls.Add(_bottom);      // bottom panel first so the tab control docks correctly
        Controls.Add(_tabs);

        ResumeLayout(false);
    }
}
