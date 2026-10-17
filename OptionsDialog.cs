namespace MockRtsp;

/// <summary>
/// Edits a copy of StreamOptions. Result is only updated when OK is clicked with valid values.
/// The layout lives in OptionsDialog.Designer.cs.
/// </summary>
public sealed partial class OptionsDialog : Form
{
    private readonly StreamOptions _working;

    public StreamOptions Result { get; private set; }

    /// <summary>Parameterless constructor for the designer.</summary>
    public OptionsDialog() : this(new StreamOptions())
    {
    }

    public OptionsDialog(StreamOptions current)
    {
        InitializeComponent();

        _working = current.Clone();
        Result = _working;

        FillComboBoxes();
        LoadValues(_working);
    }

    private void FillComboBoxes()
    {
        _publishTransport.Items.AddRange(new object[] { "tcp", "udp" });
        _ffmpegLog.Items.AddRange(new object[] { "error", "warning", "info" });
        _mtxLog.Items.AddRange(new object[] { "error", "warn", "info", "debug" });
        _transports.Items.AddRange(new object[] { "TCP and UDP", "TCP only", "UDP only" });
    }

    private void LoadValues(StreamOptions o)
    {
        _loop.Checked = o.Loop;
        SetValue(_bitrate, o.BitrateKbps);
        SetValue(_keyframe, o.KeyframeInterval);
        SetValue(_width, o.Width);
        SetValue(_height, o.Height);
        SetValue(_frameRate, o.FrameRate);
        _noBFrames.Checked = o.NoBFrames;
        _publishTransport.SelectedItem = o.PublishTransport;
        _ffmpegLog.SelectedItem = o.FfmpegLogLevel;

        _mtxLog.SelectedItem = o.MediaMtxLogLevel;
        _transports.SelectedIndex = (int)o.Transports;
        SetValue(_readTimeout, o.ReadTimeoutSeconds);
        SetValue(_writeTimeout, o.WriteTimeoutSeconds);
        _viewerUser.Text = o.ViewerUser;
        _viewerPassword.Text = o.ViewerPassword;
        _publisherUser.Text = o.PublisherUser;
        _publisherPassword.Text = o.PublisherPassword;
    }

    /// <summary>Sets a value, clamped to the box's range, so an out-of-range saved value cannot throw.</summary>
    private static void SetValue(NumericUpDown box, int value) =>
        box.Value = Math.Min(box.Maximum, Math.Max(box.Minimum, value));

    private void ApplyToOptions()
    {
        CommitTyped(_bitrate);
        CommitTyped(_keyframe);
        CommitTyped(_width);
        CommitTyped(_height);
        CommitTyped(_frameRate);
        CommitTyped(_readTimeout);
        CommitTyped(_writeTimeout);

        _working.Loop = _loop.Checked;
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

    /// <summary>
    /// A NumericUpDown only takes a typed number when focus leaves the box, so a value typed
    /// just before pressing Enter or clicking OK could be lost. Read the typed text directly.
    /// </summary>
    private static void CommitTyped(NumericUpDown box)
    {
        if (decimal.TryParse(box.Text.Trim(), System.Globalization.NumberStyles.Integer | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.CurrentCulture, out decimal typed))
        {
            box.Value = Math.Min(box.Maximum, Math.Max(box.Minimum, typed));
        }
    }

    // ---- Event handlers (wired in OptionsDialog.Designer.cs) ----

    private void OnResetClick(object? sender, EventArgs e) => LoadValues(new StreamOptions());

    private void OnOkClick(object? sender, EventArgs e)
    {
        try
        {
            ApplyToOptions();

            var problems = _working.Validate();
            if (problems.Count > 0)
            {
                MessageBox.Show(this, string.Join(Environment.NewLine, problems),
                    "Check the settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;                                 // stay open so the user can fix it
            }

            Result = _working;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Settings");
            MessageBox.Show(this, ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
