#nullable enable

namespace MockRtsp;

public sealed partial class CameraPanel
{
    private System.ComponentModel.IContainer? components = null;

    private TableLayoutPanel _layout = null!;
    private FlowLayoutPanel _header = null!;
    private Label _state = null!;
    private Label _path = null!;
    private Label _details = null!;
    private Button _startStop = null!;
    private Button _openVlc = null!;
    private Button _settings = null!;
    private Button _scanMotion = null!;
    private Label _lblOutage = null!;
    private NumericUpDown _outageSeconds = null!;
    private Button _outage = null!;
    private Button _remove = null!;
    private FlowLayoutPanel _player = null!;
    private Button _back10 = null!;
    private Button _playPause = null!;
    private Button _forward10 = null!;
    private Label _time = null!;
    private Label _lblSpeed = null!;
    private ComboBox _speed = null!;
    private Button _skipStart = null!;
    private Button _skipEnd = null!;
    private Button _zoomOut = null!;
    private Button _zoomIn = null!;
    private Button _zoomFit = null!;
    private Timeline _timeline = null!;

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
        _layout = new TableLayoutPanel();
        _header = new FlowLayoutPanel();
        _state = new Label();
        _path = new Label();
        _details = new Label();
        _startStop = new Button();
        _openVlc = new Button();
        _settings = new Button();
        _scanMotion = new Button();
        _lblOutage = new Label();
        _outageSeconds = new NumericUpDown();
        _outage = new Button();
        _remove = new Button();
        _player = new FlowLayoutPanel();
        _back10 = new Button();
        _playPause = new Button();
        _forward10 = new Button();
        _time = new Label();
        _lblSpeed = new Label();
        _speed = new ComboBox();
        _skipStart = new Button();
        _skipEnd = new Button();
        _zoomOut = new Button();
        _zoomIn = new Button();
        _zoomFit = new Button();
        _timeline = new Timeline();

        SuspendLayout();
        _layout.SuspendLayout();
        _header.SuspendLayout();
        _player.SuspendLayout();

        // ================= Header: state, path, video, camera actions =================
        _state.BackColor = Color.DimGray;
        _state.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _state.ForeColor = Color.White;
        _state.Margin = new Padding(0, 5, 8, 0);
        _state.Size = new Size(62, 20);
        _state.Text = "OFF";
        _state.TextAlign = ContentAlignment.MiddleCenter;

        _path.AutoSize = true;
        _path.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _path.Margin = new Padding(0, 4, 10, 0);
        _path.Text = "Streaming/Channels/101";

        _details.AutoSize = true;
        _details.ForeColor = SystemColors.GrayText;
        _details.Margin = new Padding(0, 7, 14, 0);
        _details.Text = "video.mp4";

        _startStop.Size = new Size(64, 28);
        _startStop.Text = "Stop";
        _openVlc.Size = new Size(96, 28);
        _openVlc.Text = "Open in VLC";
        _settings.Size = new Size(110, 28);
        _settings.Text = "Video settings...";
        _scanMotion.Size = new Size(110, 28);
        _scanMotion.Text = "Scan motion";

        _lblOutage.AutoSize = true;
        _lblOutage.Margin = new Padding(10, 7, 2, 0);
        _lblOutage.Text = "Outage (s):";
        _outageSeconds.Margin = new Padding(0, 4, 3, 0);
        _outageSeconds.Maximum = 3600;
        _outageSeconds.Minimum = 1;
        _outageSeconds.Value = 5;
        _outageSeconds.Width = 56;
        _outage.Size = new Size(72, 28);
        _outage.Text = "Simulate";

        _remove.Margin = new Padding(14, 3, 3, 3);
        _remove.Size = new Size(72, 28);
        _remove.Text = "Remove";

        _header.AutoSize = true;
        _header.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _header.Dock = DockStyle.Fill;
        _header.Margin = new Padding(0);
        _header.WrapContents = true;
        _header.Controls.AddRange(new Control[] { _state, _path, _details, _startStop, _openVlc, _settings, _scanMotion, _lblOutage, _outageSeconds, _outage, _remove });

        // ================= Player: pause, rewind, forward, speed, skips, zoom =================
        _back10.Size = new Size(60, 28);
        _back10.Text = "« 10 s";
        _playPause.Size = new Size(60, 28);
        _playPause.Text = "Pause";
        _forward10.Size = new Size(60, 28);
        _forward10.Text = "10 s »";

        _time.AutoSize = true;
        _time.Font = new Font("Consolas", 9F);
        _time.Margin = new Padding(10, 8, 10, 0);
        _time.Text = "00:00 / 00:00";

        _lblSpeed.AutoSize = true;
        _lblSpeed.Margin = new Padding(0, 8, 2, 0);
        _lblSpeed.Text = "Speed:";
        _speed.DropDownStyle = ComboBoxStyle.DropDownList;
        _speed.Items.AddRange(new object[] { "0.25x", "0.5x", "1x", "2x", "4x" });
        _speed.Margin = new Padding(0, 4, 3, 0);
        _speed.Width = 64;

        _skipStart.Margin = new Padding(14, 3, 3, 3);
        _skipStart.Size = new Size(74, 28);
        _skipStart.Text = "Skip start";
        _skipEnd.Size = new Size(74, 28);
        _skipEnd.Text = "Skip end";

        _zoomOut.Margin = new Padding(14, 3, 3, 3);
        _zoomOut.Size = new Size(30, 28);
        _zoomOut.Text = "-";
        _zoomIn.Size = new Size(30, 28);
        _zoomIn.Text = "+";
        _zoomFit.Size = new Size(44, 28);
        _zoomFit.Text = "Fit";

        _player.AutoSize = true;
        _player.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _player.Dock = DockStyle.Fill;
        _player.Margin = new Padding(0, 2, 0, 0);
        _player.WrapContents = true;
        _player.Controls.AddRange(new Control[] { _back10, _playPause, _forward10, _time, _lblSpeed, _speed, _skipStart, _skipEnd, _zoomOut, _zoomIn, _zoomFit });

        // ================= Timeline: hover to preview, click to jump, red = skipped =================
        _timeline.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _timeline.Height = 74;
        _timeline.Margin = new Padding(0, 4, 0, 0);

        // ================= Layout =================
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.Dock = DockStyle.Fill;
        _layout.RowCount = 3;
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(_header, 0, 0);
        _layout.Controls.Add(_player, 0, 1);
        _layout.Controls.Add(_timeline, 0, 2);

        // ================= Events =================
        _startStop.Click += new EventHandler(OnStartStopClick);
        _openVlc.Click += new EventHandler(OnOpenVlcClick);
        _settings.Click += new EventHandler(OnSettingsClick);
        _scanMotion.Click += new EventHandler(OnScanMotionClick);
        _outage.Click += new EventHandler(OnOutageClick);
        _remove.Click += new EventHandler(OnRemoveClick);
        _back10.Click += new EventHandler(OnBack10Click);
        _playPause.Click += new EventHandler(OnPlayPauseClick);
        _forward10.Click += new EventHandler(OnForward10Click);
        _speed.SelectedIndexChanged += new EventHandler(OnSpeedChanged);
        _skipStart.Click += new EventHandler(OnSkipStartClick);
        _skipEnd.Click += new EventHandler(OnSkipEndClick);
        _zoomOut.Click += new EventHandler(OnZoomOutClick);
        _zoomIn.Click += new EventHandler(OnZoomInClick);
        _zoomFit.Click += new EventHandler(OnZoomFitClick);
        _timeline.SeekRequested += new EventHandler<double>(OnTimelineSeek);
        _timeline.SkipRangesChanged += new EventHandler(OnTimelineSkipsChanged);

        // ================= Panel =================
        AutoScaleDimensions = new SizeF(7F, 15F);   // Segoe UI 9pt at 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Font;
        BorderStyle = BorderStyle.FixedSingle;
        Font = new Font("Segoe UI", 9F);
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(8, 6, 8, 6);
        Size = new Size(1000, 160);
        Controls.Add(_layout);

        _header.ResumeLayout(false);
        _header.PerformLayout();
        _player.ResumeLayout(false);
        _player.PerformLayout();
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
    }
}
