namespace MockRtsp;

/// <summary>
/// Edits a copy of StreamOptions. Result is only updated when OK is clicked with valid values.
/// </summary>
public sealed class OptionsDialog : Form
{
    private readonly StreamOptions _working;

    // FFmpeg
    private readonly CheckBox _loop = new() { Text = "Loop video (-stream_loop)", AutoSize = true };
    private readonly CheckBox _realTime = new() { Text = "Real-time pacing (-re)", AutoSize = true };
    private readonly CheckBox _dropAudio = new() { Text = "Drop audio (-an)", AutoSize = true };
    private readonly CheckBox _reEncode = new() { Text = "Re-encode to H.264 (libx264)", AutoSize = true };
    private readonly NumericUpDown _bitrate = new() { Minimum = 0, Maximum = 100000, Increment = 100, Width = 110 };
    private readonly NumericUpDown _keyframe = new() { Minimum = 0, Maximum = 1000, Width = 110 };
    private readonly NumericUpDown _width = new() { Minimum = 0, Maximum = 7680, Increment = 16, Width = 90 };
    private readonly NumericUpDown _height = new() { Minimum = 0, Maximum = 4320, Increment = 16, Width = 90 };
    private readonly NumericUpDown _frameRate = new() { Minimum = 0, Maximum = 240, Width = 110 };
    private readonly CheckBox _noBFrames = new() { Text = "Disable B-frames (-bf 0)", AutoSize = true };
    private readonly ComboBox _publishTransport = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _ffmpegLog = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };

    // MediaMTX
    private readonly ComboBox _mtxLog = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _transports = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly NumericUpDown _readTimeout = new() { Minimum = 1, Maximum = 300, Width = 110 };
    private readonly NumericUpDown _writeTimeout = new() { Minimum = 1, Maximum = 300, Width = 110 };
    private readonly TextBox _viewerUser = new() { Width = 180 };
    private readonly TextBox _viewerPassword = new() { Width = 180, UseSystemPasswordChar = true };
    private readonly TextBox _publisherUser = new() { Width = 180 };
    private readonly TextBox _publisherPassword = new() { Width = 180, UseSystemPasswordChar = true };

    private readonly Button _ok = new() { Text = "OK", AutoSize = true };
    private readonly Button _cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };

    public StreamOptions Result { get; private set; }

    public OptionsDialog(StreamOptions current)
    {
        _working = current.Clone();
        Result = _working;

        Text = "Stream options";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        BuildLayout();
        FillComboBoxes();
        LoadFromOptions(_working);

        _reEncode.CheckedChanged += (_, _) => UpdateEnabled();
        _ok.Click += OnOk;
        AcceptButton = _ok;
        CancelButton = _cancel;

        UpdateEnabled();
    }

    private void BuildLayout()
    {
        var table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(12),
            Dock = DockStyle.Top
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        int row = 0;

        Header(table, ref row, "FFmpeg (per camera, applies to cameras added from now on)");
        Full(table, ref row, _loop);
        Full(table, ref row, _realTime);
        Full(table, ref row, _dropAudio);
        Full(table, ref row, _reEncode);
        Pair(table, ref row, "Bitrate (kbps, 0 = default):", _bitrate);
        Pair(table, ref row, "Keyframe interval (frames, 0 = default):", _keyframe);
        Pair(table, ref row, "Size (width x height, 0 = keep):", FlowOf(_width, new Label { Text = "x", AutoSize = true, Margin = new Padding(4, 6, 4, 0) }, _height));
        Pair(table, ref row, "Frame rate (0 = keep):", _frameRate);
        Full(table, ref row, _noBFrames);
        Pair(table, ref row, "Publish transport:", _publishTransport);
        Pair(table, ref row, "FFmpeg log level:", _ffmpegLog);

        Header(table, ref row, "MediaMTX (server, applies at next server start)");
        Pair(table, ref row, "MediaMTX log level:", _mtxLog);
        Pair(table, ref row, "RTSP transports:", _transports);
        Pair(table, ref row, "Read timeout (s):", _readTimeout);
        Pair(table, ref row, "Write timeout (s):", _writeTimeout);
        Pair(table, ref row, "Viewer username (blank = none):", _viewerUser);
        Pair(table, ref row, "Viewer password:", _viewerPassword);
        Pair(table, ref row, "Publisher username (blank = none):", _publisherUser);
        Pair(table, ref row, "Publisher password:", _publisherPassword);

        Full(table, ref row, FlowOf(_ok, _cancel));

        Controls.Add(table);
    }

    private void FillComboBoxes()
    {
        _publishTransport.Items.AddRange(new object[] { "tcp", "udp" });
        _ffmpegLog.Items.AddRange(new object[] { "error", "warning", "info" });
        _mtxLog.Items.AddRange(new object[] { "error", "warn", "info", "debug" });
        _transports.Items.AddRange(new object[] { "TCP and UDP", "TCP only", "UDP only" });
    }

    private void LoadFromOptions(StreamOptions o)
    {
        _loop.Checked = o.Loop;
        _realTime.Checked = o.RealTime;
        _dropAudio.Checked = o.DropAudio;
        _reEncode.Checked = o.ReEncode;
        _bitrate.Value = o.BitrateKbps;
        _keyframe.Value = o.KeyframeInterval;
        _width.Value = o.Width;
        _height.Value = o.Height;
        _frameRate.Value = o.FrameRate;
        _noBFrames.Checked = o.NoBFrames;
        _publishTransport.SelectedItem = o.PublishTransport;
        _ffmpegLog.SelectedItem = o.FfmpegLogLevel;

        _mtxLog.SelectedItem = o.MediaMtxLogLevel;
        _transports.SelectedIndex = (int)o.Transports;
        _readTimeout.Value = o.ReadTimeoutSeconds;
        _writeTimeout.Value = o.WriteTimeoutSeconds;
        _viewerUser.Text = o.ViewerUser;
        _viewerPassword.Text = o.ViewerPassword;
        _publisherUser.Text = o.PublisherUser;
        _publisherPassword.Text = o.PublisherPassword;
    }

    private void ApplyToOptions()
    {
        _working.Loop = _loop.Checked;
        _working.RealTime = _realTime.Checked;
        _working.DropAudio = _dropAudio.Checked;
        _working.ReEncode = _reEncode.Checked;
        _working.BitrateKbps = (int)_bitrate.Value;
        _working.KeyframeInterval = (int)_keyframe.Value;
        _working.Width = (int)_width.Value;
        _working.Height = (int)_height.Value;
        _working.FrameRate = (int)_frameRate.Value;
        _working.NoBFrames = _noBFrames.Checked;
        _working.PublishTransport = _publishTransport.SelectedItem?.ToString() ?? "tcp";
        _working.FfmpegLogLevel = _ffmpegLog.SelectedItem?.ToString() ?? "warning";

        _working.MediaMtxLogLevel = _mtxLog.SelectedItem?.ToString() ?? "warn";
        _working.Transports = (RtspTransportMode)Math.Max(0, _transports.SelectedIndex);
        _working.ReadTimeoutSeconds = (int)_readTimeout.Value;
        _working.WriteTimeoutSeconds = (int)_writeTimeout.Value;
        _working.ViewerUser = _viewerUser.Text.Trim();
        _working.ViewerPassword = _viewerPassword.Text;
        _working.PublisherUser = _publisherUser.Text.Trim();
        _working.PublisherPassword = _publisherPassword.Text;
    }

    private void UpdateEnabled()
    {
        // These settings only apply when the video is re-encoded.
        bool reEncode = _reEncode.Checked;
        _bitrate.Enabled = reEncode;
        _keyframe.Enabled = reEncode;
        _width.Enabled = reEncode;
        _height.Enabled = reEncode;
        _frameRate.Enabled = reEncode;
        _noBFrames.Enabled = reEncode;
    }

    private void OnOk(object? sender, EventArgs e)
    {
        try
        {
            ApplyToOptions();

            var problems = _working.Validate();
            if (problems.Count > 0)
            {
                MessageBox.Show(this, string.Join(Environment.NewLine, problems),
                    "Check the options", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;                                 // stay open so the user can fix it
            }

            Result = _working;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Stream options");
            MessageBox.Show(this, ex.Message, "Stream options", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---- Layout helpers ----

    private static void Header(TableLayoutPanel table, ref int row, string text)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Margin = new Padding(0, 10, 0, 2)
        };
        table.Controls.Add(label, 0, row);
        table.SetColumnSpan(label, 2);
        row++;
    }

    private static void Full(TableLayoutPanel table, ref int row, Control control)
    {
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
        row++;
    }

    private static void Pair(TableLayoutPanel table, ref int row, string text, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 0)
        }, 0, row);
        table.Controls.Add(control, 1, row);
        row++;
    }

    private static FlowLayoutPanel FlowOf(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 2)
        };
        panel.Controls.AddRange(controls);
        return panel;
    }
}
