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

    public OptionsDialog(StreamOptions current) : this(current, cameraOnly: false)
    {
    }

    /// <param name="cameraOnly">
    /// True for editing one running camera's quality. Hides the MediaMTX tab, because
    /// those settings only apply when the server starts.
    /// </param>
    public OptionsDialog(StreamOptions current, bool cameraOnly)
    {
        InitializeComponent();

        if (cameraOnly)
        {
            _tabs.TabPages.Remove(_tabMtx);
            _ffIntro.Text = "How this camera's video is sent. Saving restarts the camera, so viewers see a short drop.";
            Text = "Camera quality";
        }

        _working = current.Clone();
        Result = _working;

        _reEncode.CheckedChanged += new EventHandler(OnReEncodeChanged);

        // Changing any encoding setting turns re-encoding on, so the change is never silently ignored.
        _bitrate.ValueChanged += new EventHandler(OnEncodeSettingChanged);
        _keyframe.ValueChanged += new EventHandler(OnEncodeSettingChanged);
        _width.ValueChanged += new EventHandler(OnEncodeSettingChanged);
        _height.ValueChanged += new EventHandler(OnEncodeSettingChanged);
        _frameRate.ValueChanged += new EventHandler(OnEncodeSettingChanged);
        _noBFrames.CheckedChanged += new EventHandler(OnEncodeSettingChanged);

        FillComboBoxes();
        LoadFromOptions(_working);
        UpdateEnabled();
    }

    private void FillComboBoxes()
    {
        _publishTransport.Items.AddRange(new object[] { "tcp", "udp" });
        _ffmpegLog.Items.AddRange(new object[] { "error", "warning", "info" });
        _mtxLog.Items.AddRange(new object[] { "error", "warn", "info", "debug" });
        _transports.Items.AddRange(new object[] { "TCP and UDP", "TCP only", "UDP only" });
    }

    private bool _loading;

    /// <summary>Copies settings into the controls without treating the copy as a user change.</summary>
    private void LoadFromOptions(StreamOptions o)
    {
        _loading = true;
        try
        {
            LoadValues(o);
        }
        finally
        {
            _loading = false;
        }
    }

    private void LoadValues(StreamOptions o)
    {
        _loop.Checked = o.Loop;
        _realTime.Checked = o.RealTime;
        _dropAudio.Checked = o.DropAudio;
        _reEncode.Checked = o.ReEncode;
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
        // The encoding settings stay editable. Changing one turns re-encoding on (see OnEncodeSettingChanged).
    }

    // ---- Event handlers (wired in OptionsDialog.Designer.cs and the constructor) ----

    private void OnReEncodeChanged(object? sender, EventArgs e) => UpdateEnabled();

    /// <summary>A user-made change to an encoding setting implies re-encoding, so turn it on.</summary>
    private void OnEncodeSettingChanged(object? sender, EventArgs e)
    {
        if (!_loading && !_reEncode.Checked)
            _reEncode.Checked = true;
    }

    private void OnResetClick(object? sender, EventArgs e)
    {
        LoadFromOptions(new StreamOptions());
        UpdateEnabled();
    }

    private void OnOkClick(object? sender, EventArgs e)
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
}
