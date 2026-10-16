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
        _loop = new CheckBox();
        _realTime = new CheckBox();
        _reEncode = new CheckBox();
        _keyframe = new NumericUpDown();
        _transports = new ComboBox();
        _viewerUser = new TextBox();
        _publisherUser = new TextBox();
        _bottom = new Panel();
        _reset = new Button();
        _cancel = new Button();
        _ok = new Button();
        _tabs = new TabControl();
        _tabFfmpeg = new TabPage();
        _ffGrid = new TableLayoutPanel();
        _ffIntro = new Label();
        _grpTiming = new GroupBox();
        _timingGrid = new TableLayoutPanel();
        _dropAudio = new CheckBox();
        _grpEncode = new GroupBox();
        _encodeGrid = new TableLayoutPanel();
        _reEncodeHint = new Label();
        _lblBitrate = new Label();
        _bitrate = new NumericUpDown();
        _lblKeyframe = new Label();
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
        _tabMtx = new TabPage();
        _mtxGrid = new TableLayoutPanel();
        _mtxIntro = new Label();
        _grpNetwork = new GroupBox();
        _netGrid = new TableLayoutPanel();
        _lblTransports = new Label();
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
        _lblViewerPassword = new Label();
        _viewerPassword = new TextBox();
        _grpPublisher = new GroupBox();
        _pubGrid = new TableLayoutPanel();
        _lblPublisherUser = new Label();
        _lblPublisherPassword = new Label();
        _publisherPassword = new TextBox();
        ((System.ComponentModel.ISupportInitialize)_keyframe).BeginInit();
        _bottom.SuspendLayout();
        _tabs.SuspendLayout();
        _tabFfmpeg.SuspendLayout();
        _ffGrid.SuspendLayout();
        _grpTiming.SuspendLayout();
        _timingGrid.SuspendLayout();
        _grpEncode.SuspendLayout();
        _encodeGrid.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_bitrate).BeginInit();
        _sizeRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_width).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_height).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_frameRate).BeginInit();
        _grpPublish.SuspendLayout();
        _publishGrid.SuspendLayout();
        _tabMtx.SuspendLayout();
        _mtxGrid.SuspendLayout();
        _grpNetwork.SuspendLayout();
        _netGrid.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_readTimeout).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_writeTimeout).BeginInit();
        _grpLogging.SuspendLayout();
        _logGrid.SuspendLayout();
        _grpViewer.SuspendLayout();
        _viewerGrid.SuspendLayout();
        _grpPublisher.SuspendLayout();
        _pubGrid.SuspendLayout();
        SuspendLayout();
        // 
        // _loop
        // 
        _loop.AutoSize = true;
        _loop.Checked = true;
        _loop.CheckState = CheckState.Checked;
        _loop.Location = new Point(0, 2);
        _loop.Margin = new Padding(0, 2, 0, 2);
        _loop.Name = "_loop";
        _loop.Size = new Size(187, 17);
        _loop.TabIndex = 0;
        _loop.Text = "Loop the video when it ends";
        _tips.SetToolTip(_loop, "Replay the video from the start when it reaches the end. Off stops the camera after one pass.");
        // 
        // _realTime
        // 
        _realTime.AutoSize = true;
        _realTime.Checked = true;
        _realTime.CheckState = CheckState.Checked;
        _realTime.Location = new Point(0, 23);
        _realTime.Margin = new Padding(0, 2, 0, 2);
        _realTime.Name = "_realTime";
        _realTime.Size = new Size(248, 17);
        _realTime.TabIndex = 1;
        _realTime.Text = "Send at real-time speed (like a camera)";
        _tips.SetToolTip(_realTime, "Sends frames at their normal rate. Turn off to send as fast as possible.");
        // 
        // _reEncode
        // 
        _reEncode.AutoSize = true;
        _encodeGrid.SetColumnSpan(_reEncode, 2);
        _reEncode.Location = new Point(0, 2);
        _reEncode.Margin = new Padding(0, 2, 0, 2);
        _reEncode.Name = "_reEncode";
        _reEncode.Size = new Size(193, 17);
        _reEncode.TabIndex = 0;
        _reEncode.Text = "Re-encode to H.264 (libx264)";
        _tips.SetToolTip(_reEncode, "Re-encodes the video. Required for the bitrate, keyframe, size and frame rate settings.");
        // 
        // _keyframe
        // 
        _keyframe.Location = new Point(236, 79);
        _keyframe.Margin = new Padding(0, 2, 0, 2);
        _keyframe.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _keyframe.Name = "_keyframe";
        _keyframe.Size = new Size(105, 20);
        _keyframe.TabIndex = 5;
        _tips.SetToolTip(_keyframe, "Shorter intervals let a viewer start playback sooner, at a higher bitrate.");
        // 
        // _transports
        // 
        _transports.DropDownStyle = ComboBoxStyle.DropDownList;
        _transports.Location = new Point(154, 2);
        _transports.Margin = new Padding(0, 2, 0, 2);
        _transports.Name = "_transports";
        _transports.Size = new Size(140, 21);
        _transports.TabIndex = 1;
        _tips.SetToolTip(_transports, "Which transports MediaMTX accepts from viewers. TCP is the most reliable.");
        // 
        // _viewerUser
        // 
        _viewerUser.Location = new Point(162, 2);
        _viewerUser.Margin = new Padding(0, 2, 0, 2);
        _viewerUser.Name = "_viewerUser";
        _viewerUser.Size = new Size(176, 20);
        _viewerUser.TabIndex = 1;
        _tips.SetToolTip(_viewerUser, "Set both a username and a password to require a login for viewers.");
        // 
        // _publisherUser
        // 
        _publisherUser.Location = new Point(162, 2);
        _publisherUser.Margin = new Padding(0, 2, 0, 2);
        _publisherUser.Name = "_publisherUser";
        _publisherUser.Size = new Size(176, 20);
        _publisherUser.TabIndex = 1;
        _tips.SetToolTip(_publisherUser, "Set both a username and a password to require a login for the publisher.");
        // 
        // _bottom
        // 
        _bottom.Controls.Add(_reset);
        _bottom.Controls.Add(_cancel);
        _bottom.Controls.Add(_ok);
        _bottom.Dock = DockStyle.Bottom;
        _bottom.Location = new Point(0, 433);
        _bottom.Margin = new Padding(3, 2, 3, 2);
        _bottom.Name = "_bottom";
        _bottom.Padding = new Padding(10, 8, 10, 8);
        _bottom.Size = new Size(560, 44);
        _bottom.TabIndex = 0;
        // 
        // _reset
        // 
        _reset.Location = new Point(10, 10);
        _reset.Margin = new Padding(3, 2, 3, 2);
        _reset.Name = "_reset";
        _reset.Size = new Size(105, 22);
        _reset.TabIndex = 0;
        _reset.Text = "Reset defaults";
        _reset.Click += OnResetClick;
        // 
        // _cancel
        // 
        _cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _cancel.DialogResult = DialogResult.Cancel;
        _cancel.Location = new Point(753, 10);
        _cancel.Margin = new Padding(3, 2, 3, 2);
        _cancel.Name = "_cancel";
        _cancel.Size = new Size(84, 22);
        _cancel.TabIndex = 1;
        _cancel.Text = "Cancel";
        // 
        // _ok
        // 
        _ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _ok.Location = new Point(843, 10);
        _ok.Margin = new Padding(3, 2, 3, 2);
        _ok.Name = "_ok";
        _ok.Size = new Size(84, 22);
        _ok.TabIndex = 2;
        _ok.Text = "OK";
        _ok.Click += OnOkClick;
        // 
        // _tabs
        // 
        _tabs.Controls.Add(_tabFfmpeg);
        _tabs.Controls.Add(_tabMtx);
        _tabs.Dock = DockStyle.Fill;
        _tabs.Location = new Point(0, 0);
        _tabs.Margin = new Padding(3, 2, 3, 2);
        _tabs.Name = "_tabs";
        _tabs.Padding = new Point(14, 6);
        _tabs.SelectedIndex = 0;
        _tabs.Size = new Size(560, 477);
        _tabs.TabIndex = 1;
        // 
        // _tabFfmpeg
        // 
        _tabFfmpeg.Controls.Add(_ffGrid);
        _tabFfmpeg.Location = new Point(4, 30);
        _tabFfmpeg.Margin = new Padding(3, 2, 3, 2);
        _tabFfmpeg.Name = "_tabFfmpeg";
        _tabFfmpeg.Padding = new Padding(9, 8, 9, 8);
        _tabFfmpeg.Size = new Size(552, 443);
        _tabFfmpeg.TabIndex = 0;
        _tabFfmpeg.Text = "FFmpeg (camera stream)";
        // 
        // _ffGrid
        // 
        _ffGrid.AutoSize = true;
        _ffGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _ffGrid.ColumnCount = 1;
        _ffGrid.ColumnStyles.Add(new ColumnStyle());
        _ffGrid.Controls.Add(_ffIntro, 0, 0);
        _ffGrid.Controls.Add(_grpTiming, 0, 1);
        _ffGrid.Controls.Add(_grpEncode, 0, 2);
        _ffGrid.Controls.Add(_grpPublish, 0, 3);
        _ffGrid.Dock = DockStyle.Fill;
        _ffGrid.Location = new Point(9, 8);
        _ffGrid.Margin = new Padding(3, 2, 3, 2);
        _ffGrid.Name = "_ffGrid";
        _ffGrid.RowCount = 4;
        _ffGrid.RowStyles.Add(new RowStyle());
        _ffGrid.RowStyles.Add(new RowStyle());
        _ffGrid.RowStyles.Add(new RowStyle());
        _ffGrid.RowStyles.Add(new RowStyle());
        _ffGrid.Size = new Size(534, 427);
        _ffGrid.TabIndex = 0;
        // 
        // _ffIntro
        // 
        _ffIntro.AutoSize = true;
        _ffIntro.ForeColor = SystemColors.GrayText;
        _ffIntro.Location = new Point(4, 3);
        _ffIntro.Margin = new Padding(4, 3, 4, 6);
        _ffIntro.MaximumSize = new Size(490, 0);
        _ffIntro.Name = "_ffIntro";
        _ffIntro.Size = new Size(434, 15);
        _ffIntro.TabIndex = 0;
        _ffIntro.Text = "How each camera's video is sent. Changes apply to cameras added from now on.";
        // 
        // _grpTiming
        // 
        _grpTiming.AutoSize = true;
        _grpTiming.Controls.Add(_timingGrid);
        _grpTiming.Dock = DockStyle.Fill;
        _grpTiming.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        _grpTiming.Location = new Point(3, 26);
        _grpTiming.Margin = new Padding(3, 2, 3, 2);
        _grpTiming.Name = "_grpTiming";
        _grpTiming.Padding = new Padding(9, 4, 9, 6);
        _grpTiming.Size = new Size(528, 86);
        _grpTiming.TabIndex = 1;
        _grpTiming.TabStop = false;
        _grpTiming.Text = "Looping and timing";
        // 
        // _timingGrid
        // 
        _timingGrid.AutoSize = true;
        _timingGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _timingGrid.ColumnCount = 1;
        _timingGrid.ColumnStyles.Add(new ColumnStyle());
        _timingGrid.Controls.Add(_loop, 0, 0);
        _timingGrid.Controls.Add(_realTime, 0, 1);
        _timingGrid.Controls.Add(_dropAudio, 0, 2);
        _timingGrid.Dock = DockStyle.Fill;
        _timingGrid.Location = new Point(9, 17);
        _timingGrid.Margin = new Padding(3, 2, 3, 2);
        _timingGrid.Name = "_timingGrid";
        _timingGrid.RowCount = 3;
        _timingGrid.RowStyles.Add(new RowStyle());
        _timingGrid.RowStyles.Add(new RowStyle());
        _timingGrid.RowStyles.Add(new RowStyle());
        _timingGrid.Size = new Size(510, 63);
        _timingGrid.TabIndex = 0;
        // 
        // _dropAudio
        // 
        _dropAudio.AutoSize = true;
        _dropAudio.Checked = true;
        _dropAudio.CheckState = CheckState.Checked;
        _dropAudio.Location = new Point(0, 44);
        _dropAudio.Margin = new Padding(0, 2, 0, 2);
        _dropAudio.Name = "_dropAudio";
        _dropAudio.Size = new Size(88, 17);
        _dropAudio.TabIndex = 2;
        _dropAudio.Text = "Drop audio";
        // 
        // _grpEncode
        // 
        _grpEncode.AutoSize = true;
        _grpEncode.Controls.Add(_encodeGrid);
        _grpEncode.Dock = DockStyle.Fill;
        _grpEncode.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        _grpEncode.Location = new Point(3, 116);
        _grpEncode.Margin = new Padding(3, 2, 3, 2);
        _grpEncode.Name = "_grpEncode";
        _grpEncode.Padding = new Padding(9, 4, 9, 6);
        _grpEncode.Size = new Size(528, 193);
        _grpEncode.TabIndex = 2;
        _grpEncode.TabStop = false;
        _grpEncode.Text = "Video encoding";
        // 
        // _encodeGrid
        // 
        _encodeGrid.AutoSize = true;
        _encodeGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _encodeGrid.ColumnCount = 2;
        _encodeGrid.ColumnStyles.Add(new ColumnStyle());
        _encodeGrid.ColumnStyles.Add(new ColumnStyle());
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
        _encodeGrid.Dock = DockStyle.Fill;
        _encodeGrid.Location = new Point(9, 17);
        _encodeGrid.Margin = new Padding(3, 2, 3, 2);
        _encodeGrid.Name = "_encodeGrid";
        _encodeGrid.RowCount = 7;
        _encodeGrid.RowStyles.Add(new RowStyle());
        _encodeGrid.RowStyles.Add(new RowStyle());
        _encodeGrid.RowStyles.Add(new RowStyle());
        _encodeGrid.RowStyles.Add(new RowStyle());
        _encodeGrid.RowStyles.Add(new RowStyle());
        _encodeGrid.RowStyles.Add(new RowStyle());
        _encodeGrid.RowStyles.Add(new RowStyle());
        _encodeGrid.Size = new Size(510, 170);
        _encodeGrid.TabIndex = 0;
        // 
        // _reEncodeHint
        // 
        _reEncodeHint.AutoSize = true;
        _encodeGrid.SetColumnSpan(_reEncodeHint, 2);
        _reEncodeHint.ForeColor = SystemColors.GrayText;
        _reEncodeHint.Location = new Point(18, 21);
        _reEncodeHint.Margin = new Padding(18, 0, 0, 6);
        _reEncodeHint.MaximumSize = new Size(455, 0);
        _reEncodeHint.Name = "_reEncodeHint";
        _reEncodeHint.Size = new Size(429, 26);
        _reEncodeHint.TabIndex = 1;
        _reEncodeHint.Text = "Off passes the camera video through unchanged. On enables the settings below.";
        // 
        // _lblBitrate
        // 
        _lblBitrate.AutoSize = true;
        _lblBitrate.Location = new Point(0, 57);
        _lblBitrate.Margin = new Padding(0, 4, 9, 0);
        _lblBitrate.Name = "_lblBitrate";
        _lblBitrate.Size = new Size(156, 13);
        _lblBitrate.TabIndex = 2;
        _lblBitrate.Text = "Bitrate (kbps, 0 = default):";
        // 
        // _bitrate
        // 
        _bitrate.Increment = new decimal(new int[] { 100, 0, 0, 0 });
        _bitrate.Location = new Point(236, 55);
        _bitrate.Margin = new Padding(0, 2, 0, 2);
        _bitrate.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        _bitrate.Name = "_bitrate";
        _bitrate.Size = new Size(105, 20);
        _bitrate.TabIndex = 3;
        // 
        // _lblKeyframe
        // 
        _lblKeyframe.AutoSize = true;
        _lblKeyframe.Location = new Point(0, 81);
        _lblKeyframe.Margin = new Padding(0, 4, 9, 0);
        _lblKeyframe.Name = "_lblKeyframe";
        _lblKeyframe.Size = new Size(227, 13);
        _lblKeyframe.TabIndex = 4;
        _lblKeyframe.Text = "Keyframe interval (frames, 0 = default):";
        // 
        // _lblSize
        // 
        _lblSize.AutoSize = true;
        _lblSize.Location = new Point(0, 105);
        _lblSize.Margin = new Padding(0, 4, 9, 0);
        _lblSize.Name = "_lblSize";
        _lblSize.Size = new Size(184, 13);
        _lblSize.TabIndex = 6;
        _lblSize.Text = "Size (width x height, 0 = keep):";
        // 
        // _sizeRow
        // 
        _sizeRow.AutoSize = true;
        _sizeRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sizeRow.Controls.Add(_width);
        _sizeRow.Controls.Add(_sizeX);
        _sizeRow.Controls.Add(_height);
        _sizeRow.Location = new Point(236, 101);
        _sizeRow.Margin = new Padding(0);
        _sizeRow.Name = "_sizeRow";
        _sizeRow.Size = new Size(181, 24);
        _sizeRow.TabIndex = 7;
        _sizeRow.WrapContents = false;
        // 
        // _width
        // 
        _width.Increment = new decimal(new int[] { 16, 0, 0, 0 });
        _width.Location = new Point(0, 2);
        _width.Margin = new Padding(0, 2, 0, 2);
        _width.Maximum = new decimal(new int[] { 7680, 0, 0, 0 });
        _width.Name = "_width";
        _width.Size = new Size(79, 20);
        _width.TabIndex = 0;
        // 
        // _sizeX
        // 
        _sizeX.AutoSize = true;
        _sizeX.Location = new Point(84, 4);
        _sizeX.Margin = new Padding(5, 4, 5, 0);
        _sizeX.Name = "_sizeX";
        _sizeX.Size = new Size(13, 13);
        _sizeX.TabIndex = 1;
        _sizeX.Text = "x";
        // 
        // _height
        // 
        _height.Increment = new decimal(new int[] { 16, 0, 0, 0 });
        _height.Location = new Point(102, 2);
        _height.Margin = new Padding(0, 2, 0, 2);
        _height.Maximum = new decimal(new int[] { 4320, 0, 0, 0 });
        _height.Name = "_height";
        _height.Size = new Size(79, 20);
        _height.TabIndex = 2;
        // 
        // _lblFrameRate
        // 
        _lblFrameRate.AutoSize = true;
        _lblFrameRate.Location = new Point(0, 129);
        _lblFrameRate.Margin = new Padding(0, 4, 9, 0);
        _lblFrameRate.Name = "_lblFrameRate";
        _lblFrameRate.Size = new Size(133, 13);
        _lblFrameRate.TabIndex = 8;
        _lblFrameRate.Text = "Frame rate (0 = keep):";
        // 
        // _frameRate
        // 
        _frameRate.Location = new Point(236, 127);
        _frameRate.Margin = new Padding(0, 2, 0, 2);
        _frameRate.Maximum = new decimal(new int[] { 240, 0, 0, 0 });
        _frameRate.Name = "_frameRate";
        _frameRate.Size = new Size(105, 20);
        _frameRate.TabIndex = 9;
        // 
        // _noBFrames
        // 
        _noBFrames.AutoSize = true;
        _encodeGrid.SetColumnSpan(_noBFrames, 2);
        _noBFrames.Location = new Point(0, 151);
        _noBFrames.Margin = new Padding(0, 2, 0, 2);
        _noBFrames.Name = "_noBFrames";
        _noBFrames.Size = new Size(208, 17);
        _noBFrames.TabIndex = 10;
        _noBFrames.Text = "Disable B-frames (lower latency)";
        // 
        // _grpPublish
        // 
        _grpPublish.AutoSize = true;
        _grpPublish.Controls.Add(_publishGrid);
        _grpPublish.Dock = DockStyle.Fill;
        _grpPublish.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        _grpPublish.Location = new Point(3, 313);
        _grpPublish.Margin = new Padding(3, 2, 3, 2);
        _grpPublish.Name = "_grpPublish";
        _grpPublish.Padding = new Padding(9, 4, 9, 6);
        _grpPublish.Size = new Size(528, 112);
        _grpPublish.TabIndex = 3;
        _grpPublish.TabStop = false;
        _grpPublish.Text = "Publishing";
        // 
        // _publishGrid
        // 
        _publishGrid.AutoSize = true;
        _publishGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _publishGrid.ColumnCount = 2;
        _publishGrid.ColumnStyles.Add(new ColumnStyle());
        _publishGrid.ColumnStyles.Add(new ColumnStyle());
        _publishGrid.Controls.Add(_lblPublishTransport, 0, 0);
        _publishGrid.Controls.Add(_publishTransport, 1, 0);
        _publishGrid.Controls.Add(_lblFfmpegLog, 0, 1);
        _publishGrid.Controls.Add(_ffmpegLog, 1, 1);
        _publishGrid.Dock = DockStyle.Fill;
        _publishGrid.Location = new Point(9, 17);
        _publishGrid.Margin = new Padding(3, 2, 3, 2);
        _publishGrid.Name = "_publishGrid";
        _publishGrid.RowCount = 2;
        _publishGrid.RowStyles.Add(new RowStyle());
        _publishGrid.RowStyles.Add(new RowStyle());
        _publishGrid.Size = new Size(510, 89);
        _publishGrid.TabIndex = 0;
        // 
        // _lblPublishTransport
        // 
        _lblPublishTransport.AutoSize = true;
        _lblPublishTransport.Location = new Point(0, 4);
        _lblPublishTransport.Margin = new Padding(0, 4, 9, 0);
        _lblPublishTransport.Name = "_lblPublishTransport";
        _lblPublishTransport.Size = new Size(144, 13);
        _lblPublishTransport.TabIndex = 0;
        _lblPublishTransport.Text = "Transport to MediaMTX:";
        // 
        // _publishTransport
        // 
        _publishTransport.DropDownStyle = ComboBoxStyle.DropDownList;
        _publishTransport.Location = new Point(153, 2);
        _publishTransport.Margin = new Padding(0, 2, 0, 2);
        _publishTransport.Name = "_publishTransport";
        _publishTransport.Size = new Size(140, 21);
        _publishTransport.TabIndex = 1;
        // 
        // _lblFfmpegLog
        // 
        _lblFfmpegLog.AutoSize = true;
        _lblFfmpegLog.Location = new Point(0, 29);
        _lblFfmpegLog.Margin = new Padding(0, 4, 9, 0);
        _lblFfmpegLog.Name = "_lblFfmpegLog";
        _lblFfmpegLog.Size = new Size(107, 13);
        _lblFfmpegLog.TabIndex = 2;
        _lblFfmpegLog.Text = "FFmpeg log level:";
        // 
        // _ffmpegLog
        // 
        _ffmpegLog.DropDownStyle = ComboBoxStyle.DropDownList;
        _ffmpegLog.Location = new Point(153, 27);
        _ffmpegLog.Margin = new Padding(0, 2, 0, 2);
        _ffmpegLog.Name = "_ffmpegLog";
        _ffmpegLog.Size = new Size(140, 21);
        _ffmpegLog.TabIndex = 3;
        // 
        // _tabMtx
        // 
        _tabMtx.Controls.Add(_mtxGrid);
        _tabMtx.Location = new Point(4, 30);
        _tabMtx.Margin = new Padding(3, 2, 3, 2);
        _tabMtx.Name = "_tabMtx";
        _tabMtx.Padding = new Padding(9, 8, 9, 8);
        _tabMtx.Size = new Size(167, 41);
        _tabMtx.TabIndex = 1;
        _tabMtx.Text = "MediaMTX (server)";
        // 
        // _mtxGrid
        // 
        _mtxGrid.AutoSize = true;
        _mtxGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _mtxGrid.ColumnCount = 1;
        _mtxGrid.ColumnStyles.Add(new ColumnStyle());
        _mtxGrid.Controls.Add(_mtxIntro, 0, 0);
        _mtxGrid.Controls.Add(_grpNetwork, 0, 1);
        _mtxGrid.Controls.Add(_grpLogging, 0, 2);
        _mtxGrid.Controls.Add(_grpViewer, 0, 3);
        _mtxGrid.Controls.Add(_grpPublisher, 0, 4);
        _mtxGrid.Dock = DockStyle.Fill;
        _mtxGrid.Location = new Point(9, 8);
        _mtxGrid.Margin = new Padding(3, 2, 3, 2);
        _mtxGrid.Name = "_mtxGrid";
        _mtxGrid.RowCount = 5;
        _mtxGrid.RowStyles.Add(new RowStyle());
        _mtxGrid.RowStyles.Add(new RowStyle());
        _mtxGrid.RowStyles.Add(new RowStyle());
        _mtxGrid.RowStyles.Add(new RowStyle());
        _mtxGrid.RowStyles.Add(new RowStyle());
        _mtxGrid.Size = new Size(149, 25);
        _mtxGrid.TabIndex = 0;
        // 
        // _mtxIntro
        // 
        _mtxIntro.AutoSize = true;
        _mtxIntro.ForeColor = SystemColors.GrayText;
        _mtxIntro.Location = new Point(4, 3);
        _mtxIntro.Margin = new Padding(4, 3, 4, 6);
        _mtxIntro.MaximumSize = new Size(490, 0);
        _mtxIntro.Name = "_mtxIntro";
        _mtxIntro.Size = new Size(334, 15);
        _mtxIntro.TabIndex = 0;
        _mtxIntro.Text = "The RTSP server. Changes apply the next time the server starts.";
        // 
        // _grpNetwork
        // 
        _grpNetwork.AutoSize = true;
        _grpNetwork.Controls.Add(_netGrid);
        _grpNetwork.Dock = DockStyle.Fill;
        _grpNetwork.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        _grpNetwork.Location = new Point(3, 26);
        _grpNetwork.Margin = new Padding(3, 2, 3, 2);
        _grpNetwork.Name = "_grpNetwork";
        _grpNetwork.Padding = new Padding(9, 4, 9, 6);
        _grpNetwork.Size = new Size(356, 96);
        _grpNetwork.TabIndex = 1;
        _grpNetwork.TabStop = false;
        _grpNetwork.Text = "Network and timeouts";
        // 
        // _netGrid
        // 
        _netGrid.AutoSize = true;
        _netGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _netGrid.ColumnCount = 2;
        _netGrid.ColumnStyles.Add(new ColumnStyle());
        _netGrid.ColumnStyles.Add(new ColumnStyle());
        _netGrid.Controls.Add(_lblTransports, 0, 0);
        _netGrid.Controls.Add(_transports, 1, 0);
        _netGrid.Controls.Add(_lblReadTimeout, 0, 1);
        _netGrid.Controls.Add(_readTimeout, 1, 1);
        _netGrid.Controls.Add(_lblWriteTimeout, 0, 2);
        _netGrid.Controls.Add(_writeTimeout, 1, 2);
        _netGrid.Dock = DockStyle.Fill;
        _netGrid.Location = new Point(9, 17);
        _netGrid.Margin = new Padding(3, 2, 3, 2);
        _netGrid.Name = "_netGrid";
        _netGrid.RowCount = 3;
        _netGrid.RowStyles.Add(new RowStyle());
        _netGrid.RowStyles.Add(new RowStyle());
        _netGrid.RowStyles.Add(new RowStyle());
        _netGrid.Size = new Size(338, 73);
        _netGrid.TabIndex = 0;
        // 
        // _lblTransports
        // 
        _lblTransports.AutoSize = true;
        _lblTransports.Location = new Point(0, 4);
        _lblTransports.Margin = new Padding(0, 4, 9, 0);
        _lblTransports.Name = "_lblTransports";
        _lblTransports.Size = new Size(104, 13);
        _lblTransports.TabIndex = 0;
        _lblTransports.Text = "RTSP transports:";
        // 
        // _lblReadTimeout
        // 
        _lblReadTimeout.AutoSize = true;
        _lblReadTimeout.Location = new Point(0, 29);
        _lblReadTimeout.Margin = new Padding(0, 4, 9, 0);
        _lblReadTimeout.Name = "_lblReadTimeout";
        _lblReadTimeout.Size = new Size(145, 13);
        _lblReadTimeout.TabIndex = 2;
        _lblReadTimeout.Text = "Read timeout (seconds):";
        // 
        // _readTimeout
        // 
        _readTimeout.Location = new Point(154, 27);
        _readTimeout.Margin = new Padding(0, 2, 0, 2);
        _readTimeout.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
        _readTimeout.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _readTimeout.Name = "_readTimeout";
        _readTimeout.Size = new Size(105, 20);
        _readTimeout.TabIndex = 3;
        _readTimeout.Value = new decimal(new int[] { 10, 0, 0, 0 });
        // 
        // _lblWriteTimeout
        // 
        _lblWriteTimeout.AutoSize = true;
        _lblWriteTimeout.Location = new Point(0, 53);
        _lblWriteTimeout.Margin = new Padding(0, 4, 9, 0);
        _lblWriteTimeout.Name = "_lblWriteTimeout";
        _lblWriteTimeout.Size = new Size(145, 13);
        _lblWriteTimeout.TabIndex = 4;
        _lblWriteTimeout.Text = "Write timeout (seconds):";
        // 
        // _writeTimeout
        // 
        _writeTimeout.Location = new Point(154, 51);
        _writeTimeout.Margin = new Padding(0, 2, 0, 2);
        _writeTimeout.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
        _writeTimeout.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _writeTimeout.Name = "_writeTimeout";
        _writeTimeout.Size = new Size(105, 20);
        _writeTimeout.TabIndex = 5;
        _writeTimeout.Value = new decimal(new int[] { 10, 0, 0, 0 });
        // 
        // _grpLogging
        // 
        _grpLogging.AutoSize = true;
        _grpLogging.Controls.Add(_logGrid);
        _grpLogging.Dock = DockStyle.Fill;
        _grpLogging.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        _grpLogging.Location = new Point(3, 126);
        _grpLogging.Margin = new Padding(3, 2, 3, 2);
        _grpLogging.Name = "_grpLogging";
        _grpLogging.Padding = new Padding(9, 4, 9, 6);
        _grpLogging.Size = new Size(356, 48);
        _grpLogging.TabIndex = 2;
        _grpLogging.TabStop = false;
        _grpLogging.Text = "Logging";
        // 
        // _logGrid
        // 
        _logGrid.AutoSize = true;
        _logGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _logGrid.ColumnCount = 2;
        _logGrid.ColumnStyles.Add(new ColumnStyle());
        _logGrid.ColumnStyles.Add(new ColumnStyle());
        _logGrid.Controls.Add(_lblMtxLog, 0, 0);
        _logGrid.Controls.Add(_mtxLog, 1, 0);
        _logGrid.Dock = DockStyle.Fill;
        _logGrid.Location = new Point(9, 17);
        _logGrid.Margin = new Padding(3, 2, 3, 2);
        _logGrid.Name = "_logGrid";
        _logGrid.RowCount = 1;
        _logGrid.RowStyles.Add(new RowStyle());
        _logGrid.Size = new Size(338, 25);
        _logGrid.TabIndex = 0;
        // 
        // _lblMtxLog
        // 
        _lblMtxLog.AutoSize = true;
        _lblMtxLog.Location = new Point(0, 4);
        _lblMtxLog.Margin = new Padding(0, 4, 9, 0);
        _lblMtxLog.Name = "_lblMtxLog";
        _lblMtxLog.Size = new Size(123, 13);
        _lblMtxLog.TabIndex = 0;
        _lblMtxLog.Text = "MediaMTX log level:";
        // 
        // _mtxLog
        // 
        _mtxLog.DropDownStyle = ComboBoxStyle.DropDownList;
        _mtxLog.Location = new Point(132, 2);
        _mtxLog.Margin = new Padding(0, 2, 0, 2);
        _mtxLog.Name = "_mtxLog";
        _mtxLog.Size = new Size(140, 21);
        _mtxLog.TabIndex = 1;
        // 
        // _grpViewer
        // 
        _grpViewer.AutoSize = true;
        _grpViewer.Controls.Add(_viewerGrid);
        _grpViewer.Dock = DockStyle.Fill;
        _grpViewer.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        _grpViewer.Location = new Point(3, 178);
        _grpViewer.Margin = new Padding(3, 2, 3, 2);
        _grpViewer.Name = "_grpViewer";
        _grpViewer.Padding = new Padding(9, 4, 9, 6);
        _grpViewer.Size = new Size(356, 71);
        _grpViewer.TabIndex = 3;
        _grpViewer.TabStop = false;
        _grpViewer.Text = "Viewer login (optional, required to watch the stream)";
        // 
        // _viewerGrid
        // 
        _viewerGrid.AutoSize = true;
        _viewerGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _viewerGrid.ColumnCount = 2;
        _viewerGrid.ColumnStyles.Add(new ColumnStyle());
        _viewerGrid.ColumnStyles.Add(new ColumnStyle());
        _viewerGrid.Controls.Add(_lblViewerUser, 0, 0);
        _viewerGrid.Controls.Add(_viewerUser, 1, 0);
        _viewerGrid.Controls.Add(_lblViewerPassword, 0, 1);
        _viewerGrid.Controls.Add(_viewerPassword, 1, 1);
        _viewerGrid.Dock = DockStyle.Fill;
        _viewerGrid.Location = new Point(9, 17);
        _viewerGrid.Margin = new Padding(3, 2, 3, 2);
        _viewerGrid.Name = "_viewerGrid";
        _viewerGrid.RowCount = 2;
        _viewerGrid.RowStyles.Add(new RowStyle());
        _viewerGrid.RowStyles.Add(new RowStyle());
        _viewerGrid.Size = new Size(338, 48);
        _viewerGrid.TabIndex = 0;
        // 
        // _lblViewerUser
        // 
        _lblViewerUser.AutoSize = true;
        _lblViewerUser.Location = new Point(0, 4);
        _lblViewerUser.Margin = new Padding(0, 4, 9, 0);
        _lblViewerUser.Name = "_lblViewerUser";
        _lblViewerUser.Size = new Size(153, 13);
        _lblViewerUser.TabIndex = 0;
        _lblViewerUser.Text = "Username (blank = none):";
        // 
        // _lblViewerPassword
        // 
        _lblViewerPassword.AutoSize = true;
        _lblViewerPassword.Location = new Point(0, 28);
        _lblViewerPassword.Margin = new Padding(0, 4, 9, 0);
        _lblViewerPassword.Name = "_lblViewerPassword";
        _lblViewerPassword.Size = new Size(65, 13);
        _lblViewerPassword.TabIndex = 2;
        _lblViewerPassword.Text = "Password:";
        // 
        // _viewerPassword
        // 
        _viewerPassword.Location = new Point(162, 26);
        _viewerPassword.Margin = new Padding(0, 2, 0, 2);
        _viewerPassword.Name = "_viewerPassword";
        _viewerPassword.Size = new Size(176, 20);
        _viewerPassword.TabIndex = 3;
        _viewerPassword.UseSystemPasswordChar = true;
        // 
        // _grpPublisher
        // 
        _grpPublisher.AutoSize = true;
        _grpPublisher.Controls.Add(_pubGrid);
        _grpPublisher.Dock = DockStyle.Fill;
        _grpPublisher.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        _grpPublisher.Location = new Point(3, 253);
        _grpPublisher.Margin = new Padding(3, 2, 3, 2);
        _grpPublisher.Name = "_grpPublisher";
        _grpPublisher.Padding = new Padding(9, 4, 9, 6);
        _grpPublisher.Size = new Size(356, 71);
        _grpPublisher.TabIndex = 4;
        _grpPublisher.TabStop = false;
        _grpPublisher.Text = "Publisher login (optional, required to send the stream)";
        // 
        // _pubGrid
        // 
        _pubGrid.AutoSize = true;
        _pubGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _pubGrid.ColumnCount = 2;
        _pubGrid.ColumnStyles.Add(new ColumnStyle());
        _pubGrid.ColumnStyles.Add(new ColumnStyle());
        _pubGrid.Controls.Add(_lblPublisherUser, 0, 0);
        _pubGrid.Controls.Add(_publisherUser, 1, 0);
        _pubGrid.Controls.Add(_lblPublisherPassword, 0, 1);
        _pubGrid.Controls.Add(_publisherPassword, 1, 1);
        _pubGrid.Dock = DockStyle.Fill;
        _pubGrid.Location = new Point(9, 17);
        _pubGrid.Margin = new Padding(3, 2, 3, 2);
        _pubGrid.Name = "_pubGrid";
        _pubGrid.RowCount = 2;
        _pubGrid.RowStyles.Add(new RowStyle());
        _pubGrid.RowStyles.Add(new RowStyle());
        _pubGrid.Size = new Size(338, 48);
        _pubGrid.TabIndex = 0;
        // 
        // _lblPublisherUser
        // 
        _lblPublisherUser.AutoSize = true;
        _lblPublisherUser.Location = new Point(0, 4);
        _lblPublisherUser.Margin = new Padding(0, 4, 9, 0);
        _lblPublisherUser.Name = "_lblPublisherUser";
        _lblPublisherUser.Size = new Size(153, 13);
        _lblPublisherUser.TabIndex = 0;
        _lblPublisherUser.Text = "Username (blank = none):";
        // 
        // _lblPublisherPassword
        // 
        _lblPublisherPassword.AutoSize = true;
        _lblPublisherPassword.Location = new Point(0, 28);
        _lblPublisherPassword.Margin = new Padding(0, 4, 9, 0);
        _lblPublisherPassword.Name = "_lblPublisherPassword";
        _lblPublisherPassword.Size = new Size(65, 13);
        _lblPublisherPassword.TabIndex = 2;
        _lblPublisherPassword.Text = "Password:";
        // 
        // _publisherPassword
        // 
        _publisherPassword.Location = new Point(162, 26);
        _publisherPassword.Margin = new Padding(0, 2, 0, 2);
        _publisherPassword.Name = "_publisherPassword";
        _publisherPassword.Size = new Size(176, 20);
        _publisherPassword.TabIndex = 3;
        _publisherPassword.UseSystemPasswordChar = true;
        // 
        // OptionsDialog
        // 
        AcceptButton = _ok;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = _cancel;
        ClientSize = new Size(560, 477);
        Controls.Add(_bottom);
        Controls.Add(_tabs);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Margin = new Padding(3, 2, 3, 2);
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "OptionsDialog";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Stream options";
        ((System.ComponentModel.ISupportInitialize)_keyframe).EndInit();
        _bottom.ResumeLayout(false);
        _tabs.ResumeLayout(false);
        _tabFfmpeg.ResumeLayout(false);
        _tabFfmpeg.PerformLayout();
        _ffGrid.ResumeLayout(false);
        _ffGrid.PerformLayout();
        _grpTiming.ResumeLayout(false);
        _grpTiming.PerformLayout();
        _timingGrid.ResumeLayout(false);
        _timingGrid.PerformLayout();
        _grpEncode.ResumeLayout(false);
        _grpEncode.PerformLayout();
        _encodeGrid.ResumeLayout(false);
        _encodeGrid.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_bitrate).EndInit();
        _sizeRow.ResumeLayout(false);
        _sizeRow.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_width).EndInit();
        ((System.ComponentModel.ISupportInitialize)_height).EndInit();
        ((System.ComponentModel.ISupportInitialize)_frameRate).EndInit();
        _grpPublish.ResumeLayout(false);
        _grpPublish.PerformLayout();
        _publishGrid.ResumeLayout(false);
        _publishGrid.PerformLayout();
        _tabMtx.ResumeLayout(false);
        _tabMtx.PerformLayout();
        _mtxGrid.ResumeLayout(false);
        _mtxGrid.PerformLayout();
        _grpNetwork.ResumeLayout(false);
        _grpNetwork.PerformLayout();
        _netGrid.ResumeLayout(false);
        _netGrid.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_readTimeout).EndInit();
        ((System.ComponentModel.ISupportInitialize)_writeTimeout).EndInit();
        _grpLogging.ResumeLayout(false);
        _grpLogging.PerformLayout();
        _logGrid.ResumeLayout(false);
        _logGrid.PerformLayout();
        _grpViewer.ResumeLayout(false);
        _grpViewer.PerformLayout();
        _viewerGrid.ResumeLayout(false);
        _viewerGrid.PerformLayout();
        _grpPublisher.ResumeLayout(false);
        _grpPublisher.PerformLayout();
        _pubGrid.ResumeLayout(false);
        _pubGrid.PerformLayout();
        ResumeLayout(false);
    }
}
