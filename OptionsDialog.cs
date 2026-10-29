namespace MockRtsp;

/// <summary>
/// Edits a copy of StreamOptions. Result is only updated when OK is clicked with valid values.
/// Two uses:
///   main settings   defaults for videos, plus the server
///   one video       only the settings a video can have of its own; "Use defaults" fills in
///                   the main defaults
/// The layout lives in OptionsDialog.Designer.cs.
/// </summary>
public sealed partial class OptionsDialog : Form
{
    private readonly StreamOptions _working;
    private readonly StreamOptions? _defaults;      // set for one video; what "Use defaults" loads

    public StreamOptions Result { get; private set; }

    /// <summary>Parameterless constructor for the designer.</summary>
    public OptionsDialog() : this(new StreamOptions())
    {
    }

    /// <summary>The main settings: defaults for videos, and the server.</summary>
    public OptionsDialog(StreamOptions current) : this(current, null, null)
    {
    }

    /// <summary>
    /// One video's settings when videoName is given: current holds the settings in force for it,
    /// defaults the main defaults.
    /// </summary>
    public OptionsDialog(StreamOptions current, string? videoName, StreamOptions? defaults)
    {
        InitializeComponent();

        _working = current.Clone();
        _defaults = defaults?.Clone();
        Result = _working;

        if (videoName is not null)
            ShowAsVideoSettings(videoName);

        FillComboBoxes();
        LoadValues(_working);
    }

    /// <summary>Only the settings a single video can have: no server tab, no app-wide options.</summary>
    private void ShowAsVideoSettings(string videoName)
    {
        Text = "Settings for " + videoName;
        _tabs.TabPages.Remove(_tabMtx);
        _tabFfmpeg.Text = videoName;
        _ffIntro.Text = $"Settings for this video only, saved next to it in {videoName}.mockrtsp.json. "
                        + "Cameras playing it restart with the new settings, keeping position, pause and speed. "
                        + "New motion settings pick skips again from the saved scan, without scanning again.";
        _reset.Text = "Use defaults";

        // App-wide options stay in the main settings.
        _autoScan.Visible = false;
        _hdrPublish.Visible = false;
        _lblPublishTransport.Visible = false;
        _publishTransport.Visible = false;
        _lblFfmpegLog.Visible = false;
        _ffmpegLog.Visible = false;
        _hdrSubstream.Visible = false;
        _substream.Visible = false;
        _lblSubWidth.Visible = false;
        _subWidth.Visible = false;
        _lblSubRate.Visible = false;
        _subRate.Visible = false;
        _lblSubBitrate.Visible = false;
        _subBitrate.Visible = false;
        ClientSize = new Size(ClientSize.Width, ClientSize.Height - 130);
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
        _autoScan.Checked = o.AutoScanMotion;
        SetValue(_motionThreshold, o.MotionThresholdPercent);
        SetValue(_minStill, o.MinStillSeconds);
        SetValue(_motionPadding, o.MotionPaddingSeconds);
        SetValue(_bitrate, o.BitrateKbps);
        SetValue(_keyframe, o.KeyframeInterval);
        SetValue(_width, o.Width);
        SetValue(_height, o.Height);
        SetValue(_frameRate, o.FrameRate);
        _noBFrames.Checked = o.NoBFrames;
        _publishTransport.SelectedItem = o.PublishTransport;
        _ffmpegLog.SelectedItem = o.FfmpegLogLevel;
        _substream.Checked = o.Substream;
        SetValue(_subWidth, o.SubstreamWidth);
        SetValue(_subRate, o.SubstreamFrameRate);
        SetValue(_subBitrate, o.SubstreamBitrateKbps);
        _groundTruthFolder.Text = o.GroundTruthFolder;

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

    private static void SetValue(NumericUpDown box, double value) =>
        box.Value = Math.Min(box.Maximum, Math.Max(box.Minimum, (decimal)value));

    private void ApplyToOptions()
    {
        CommitTyped(_bitrate);
        CommitTyped(_keyframe);
        CommitTyped(_width);
        CommitTyped(_height);
        CommitTyped(_frameRate);
        CommitTyped(_readTimeout);
        CommitTyped(_motionThreshold);
        CommitTyped(_minStill);
        CommitTyped(_motionPadding);
        CommitTyped(_writeTimeout);
        CommitTyped(_subWidth);
        CommitTyped(_subRate);
        CommitTyped(_subBitrate);

        _working.Loop = _loop.Checked;
        _working.AutoScanMotion = _autoScan.Checked;
        _working.MotionThresholdPercent = (double)_motionThreshold.Value;
        _working.MinStillSeconds = (double)_minStill.Value;
        _working.MotionPaddingSeconds = (double)_motionPadding.Value;
        _working.BitrateKbps = (int)_bitrate.Value;
        _working.KeyframeInterval = (int)_keyframe.Value;
        _working.Width = (int)_width.Value;
        _working.Height = (int)_height.Value;
        _working.FrameRate = (int)_frameRate.Value;
        _working.NoBFrames = _noBFrames.Checked;
        _working.PublishTransport = _publishTransport.SelectedItem?.ToString() ?? "tcp";
        _working.FfmpegLogLevel = _ffmpegLog.SelectedItem?.ToString() ?? "warning";
        _working.Substream = _substream.Checked;
        _working.SubstreamWidth = (int)_subWidth.Value & ~1;
        _working.SubstreamFrameRate = (int)_subRate.Value;
        _working.SubstreamBitrateKbps = (int)_subBitrate.Value;
        _working.GroundTruthFolder = _groundTruthFolder.Text.Trim();

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
        if (decimal.TryParse(box.Text.Trim(), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out decimal typed))
        {
            box.Value = Math.Min(box.Maximum, Math.Max(box.Minimum, typed));
        }
    }

    // ---- Event handlers (wired in OptionsDialog.Designer.cs) ----

    private void OnResetClick(object? sender, EventArgs e) => LoadValues(_defaults ?? new StreamOptions());

    private void OnBrowseGroundTruthClick(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Folder for the ground-truth log",
            SelectedPath = Directory.Exists(_groundTruthFolder.Text) ? _groundTruthFolder.Text : GroundTruthLog.DefaultFolder
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _groundTruthFolder.Text = dialog.SelectedPath;
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
