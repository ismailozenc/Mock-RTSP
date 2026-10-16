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

    // ---- Event handlers (wired in OptionsDialog.Designer.cs and the constructor) ----

    private void OnReEncodeChanged(object? sender, EventArgs e) => UpdateEnabled();

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
