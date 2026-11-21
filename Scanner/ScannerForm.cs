namespace MockRtsp;

/// <summary>
/// The scanner's main window: a folder of videos with what each has (gaps, plates read, labels),
/// a queue that scans them one after another (gaps, then plates and directions part by part), and
/// the editor of the selected video, which follows a running scan live and stays editable.
/// Everything is saved next to the videos at once, where the RTSP app picks it up.
/// The layout lives in ScannerForm.Designer.cs.
/// </summary>
public sealed partial class ScannerForm : Form
{
    private readonly ScannerOptions _options;
    private readonly string? _ffmpeg;
    private readonly string _models;
    private readonly ScanQueue? _queue;
    private readonly Dictionary<string, ScanVideo> _open = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileState> _files = new(StringComparer.OrdinalIgnoreCase);
    private int _folderGeneration;
    private bool _loadingOptions;

    /// <summary>What the list knows about a video that is not open: from its files.</summary>
    private sealed record FileState(VideoInfo? Info, SavedVideo? Saved, int? Cars, string? Error);

    public ScannerForm()
    {
        InitializeComponent();

        _options = ScannerOptions.Load();
        _ffmpeg = Toolchain.Find(Toolchain.FfmpegFolder, "ffmpeg.exe");
        _models = Path.Combine(AppContext.BaseDirectory, "models");
        _editor.GapDefaults = () => _options.GapDefaults;
        _editor.Message += (_, message) => Log(message);

        _loadingOptions = true;
        _gapsAgain.Checked = _options.GapsAgain;
        _platesAgain.Checked = _options.PlatesAgain;
        _defThreshold.Value = (decimal)_options.GapThresholdPercent;
        _defMinGap.Value = (decimal)_options.MinGapSeconds;
        _defKeep.Value = (decimal)_options.KeepAroundSeconds;
        _loadingOptions = false;

        if (_ffmpeg is null)
            Log($"FFmpeg not found. Put ffmpeg.exe in the '{Toolchain.FfmpegFolder}' folder next to MockRtspScanner.exe; nothing can be scanned or played until then.");
        else
        {
            Log($"FFmpeg: {_ffmpeg}");
            _queue = new ScanQueue(this, _ffmpeg, _models, () => _options.GapDefaults);
            _queue.StateChanged += OnQueueChanged;
            _queue.Message += (_, message) => Log(message);
        }

        if (!File.Exists(Path.Combine(_models, PlateScanSettings.DetectorFile)))
            Log($"The plate models are missing in {_models}: gaps can be found, but plates cannot be read.");

        _folder.Items.AddRange(_options.RecentFolders.Cast<object>().ToArray());
        UpdateButtons();
        _timer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_options.Folder.Length > 0 && Directory.Exists(_options.Folder))
            Safe(() => LoadFolder(_options.Folder), "Open folder");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_queue is { IsBusy: true } && e.CloseReason == CloseReason.UserClosing
            && MessageBox.Show(this, "A scan is running. Stop it and close? What was found so far is kept.", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _editor.Close();
        _queue?.Dispose();
        _options.Save();
        foreach (var video in _open.Values)
            video.Dispose();
        base.OnFormClosing(e);
    }

    // ===================================================================== folder

    private void OnBrowseClick(object? sender, EventArgs e) => Safe(() =>
    {
        using var dialog = new FolderBrowserDialog { Description = "Folder with the videos to scan", UseDescriptionForTitle = true };
        if (Directory.Exists(_options.Folder))
            dialog.SelectedPath = _options.Folder;
        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadFolder(dialog.SelectedPath);
    }, "Browse");

    private void OnFolderPicked(object? sender, EventArgs e) => Safe(() =>
    {
        if (_folder.SelectedItem is string folder)
            LoadFolder(folder);
    }, "Open folder");

    private void OnFolderKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;
        e.SuppressKeyPress = true;
        Safe(() => LoadFolder(_folder.Text), "Open folder");
    }

    private void OnRefreshClick(object? sender, EventArgs e) => Safe(() =>
    {
        if (_options.Folder.Length > 0)
            LoadFolder(_options.Folder);
    }, "Refresh");

    /// <summary>Lists the video files of a folder, then reads each one's details and files in the background.</summary>
    private void LoadFolder(string folder)
    {
        folder = folder.Trim();
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"The folder '{folder}' does not exist.");

        folder = Path.GetFullPath(folder);
        _options.Remember(folder);
        _options.Save();
        _folder.Items.Clear();
        _folder.Items.AddRange(_options.RecentFolders.Cast<object>().ToArray());
        _folder.Text = folder;

        int generation = ++_folderGeneration;
        var files = Directory.EnumerateFiles(folder).Where(VideoInfo.IsVideoFile)
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();

        _files.Clear();
        _videos.BeginUpdate();
        try
        {
            _videos.Items.Clear();
            foreach (var file in files)
                _videos.Items.Add(new ListViewItem(new[] { Path.GetFileName(file), "...", "...", "", "", "" }) { Tag = file, ForeColor = SystemColors.GrayText });
        }
        finally
        {
            _videos.EndUpdate();
        }

        _folderInfo.Text = files.Count == 0 ? "No video files in this folder." : $"Checking {files.Count} file(s)...";
        Log($"Video folder: {folder} ({files.Count} video file(s)).");
        if (files.Count > 0 && _ffmpeg is { } ffmpeg)
            _ = ProbeFolderAsync(files, ffmpeg, generation);
    }

    private async Task ProbeFolderAsync(List<string> files, string ffmpeg, int generation)
    {
        int playable = 0;
        foreach (var file in files)
        {
            var state = await Task.Run(() => Probe(ffmpeg, file));
            if (generation != _folderGeneration || IsDisposed)
                return;

            if (state.Info is null)
            {
                Log($"Not playable, so not listed: {Path.GetFileName(file)} ({state.Error})");
                FindItem(file)?.Remove();
                continue;
            }

            playable++;
            _files[file] = state;
            UpdateRow(file);
        }

        _folderInfo.Text = $"{playable} playable video(s).";
        UpdateButtons();
    }

    private static FileState Probe(string ffmpeg, string file)
    {
        VideoInfo info;
        try
        {
            info = VideoInfo.Probe(ffmpeg, file);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return new FileState(null, null, null, ex.Message.Split('\n')[0].Trim());
        }

        SavedVideo? saved = null;
        string? error = null;
        int? cars = null;
        try
        {
            saved = VideoSidecar.Load(file, info.DurationSeconds);
            cars = PlateScanFile.Load(file)?.Cars.Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error = ex.Message;
        }

        return new FileState(info, saved, cars, error);
    }

    // ===================================================================== the list

    private ListViewItem? FindItem(string file) =>
        _videos.Items.Cast<ListViewItem>().FirstOrDefault(i => string.Equals((string)i.Tag!, file, StringComparison.OrdinalIgnoreCase));

    /// <summary>A row from the open video (or from its files when it is not open) and its place in the queue.</summary>
    private void UpdateRow(string file)
    {
        var item = FindItem(file);
        if (item is null)
            return;

        string length, gaps, plates, labels, scan = "";
        string? tip = null;
        if (_open.TryGetValue(file, out var video))
        {
            length = PlateMarks.FormatTime(video.Duration);
            gaps = video.LoadError is not null ? "file damaged" : !video.Scanned ? "not scanned" : video.Skips.Count == 0 ? "none" : $"{video.Skips.Count}";
            plates = video.Reading is { } r ? $"{r.Cars.Count} car(s)" : "-";
            var summary = video.Summary;
            int check = video.ToCheckCount;
            labels = $"{summary.Labelled}/{summary.Parts}" + (check > 0 ? $", {check} to check" : "");
            tip = video.LoadError;
            if (_queue?.JobOf(video) is { } job)
                scan = job.Status;
        }
        else if (_files.TryGetValue(file, out var state) && state.Info is { } info)
        {
            length = PlateMarks.FormatTime(info.DurationSeconds);
            if (state.Saved is { } saved)
            {
                gaps = !saved.Scanned ? "not scanned" : saved.Skips.Count == 0 ? "none" : $"{saved.Skips.Count}";
                var summary = VehicleLabels.Summarize(saved.Skips, saved.Vehicles, saved.EmptyParts, info.DurationSeconds);
                labels = $"{summary.Labelled}/{summary.Parts}";
            }
            else
            {
                gaps = "file damaged";
                labels = "?";
                tip = state.Error;
            }

            plates = state.Cars is int cars ? $"{cars} car(s)" : "-";
        }
        else
        {
            return;
        }

        item.ForeColor = SystemColors.WindowText;
        item.SubItems[1].Text = length;
        item.SubItems[2].Text = gaps;
        item.SubItems[3].Text = plates;
        item.SubItems[4].Text = labels;
        item.SubItems[5].Text = scan;
        item.ToolTipText = tip ?? scan;
    }

    private List<string> SelectedFiles() => _videos.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag!).ToList();

    /// <summary>The video of a file, opened now if needed (and kept open while the app runs).</summary>
    private ScanVideo OpenVideo(string file)
    {
        if (_open.TryGetValue(file, out var video))
            return video;

        if (_ffmpeg is null)
            throw new InvalidOperationException($"ffmpeg.exe not found. Put it in the '{Toolchain.FfmpegFolder}' folder next to MockRtspScanner.exe.");

        video = ScanVideo.Open(_ffmpeg, file);
        video.Message += (_, message) => Log(message);
        video.Changed += (_, _) => UpdateRow(file);
        _open[file] = video;
        if (video.LoadError is not null)
            Log($"ERROR: {video.Name}: its file cannot be read ({video.LoadError}). Nothing is changed or scanned until it is fixed or deleted.");
        return video;
    }

    private void OnVideoSelected(object? sender, EventArgs e) => Safe(() =>
    {
        UpdateButtons();
        var files = SelectedFiles();
        if (files.Count != 1 || !_files.ContainsKey(files[0]))
            return;

        var video = OpenVideo(files[0]);
        _editor.SetVideo(video, _ffmpeg);
        _editor.ShowTitle(_queue?.JobOf(video) is { } job ? job.Status : null);
        UpdateRow(files[0]);
    }, "Open video");

    // ===================================================================== the queue

    private void OnScanClick(object? sender, EventArgs e) => Safe(() => Queue(SelectedFiles()), "Scan");

    private void OnScanAllClick(object? sender, EventArgs e) => Safe(() =>
    {
        var files = _files.Where(f => f.Value.Info is not null && !Done(f.Key)).Select(f => f.Key)
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 0)
            Log("Every video has its gaps found and its plates read. Tick Find gaps again or Read plates again to scan again.");
        Queue(files);
    }, "Scan all");

    /// <summary>True when the video has its gaps and plates already.</summary>
    private bool Done(string file) =>
        _open.TryGetValue(file, out var video)
            ? video.Scanned && video.Reading is not null
            : _files.TryGetValue(file, out var state) && state.Saved is { Scanned: true } && state.Cars is not null;

    private void Queue(List<string> files)
    {
        if (_queue is null)
            throw new InvalidOperationException($"ffmpeg.exe not found. Put it in the '{Toolchain.FfmpegFolder}' folder next to MockRtspScanner.exe.");

        foreach (var file in files.Where(_files.ContainsKey))
        {
            var video = OpenVideo(file);
            _queue.Add(video, _gapsAgain.Checked, _platesAgain.Checked);
            UpdateRow(file);
        }

        UpdateButtons();
    }

    private void OnStopClick(object? sender, EventArgs e) => Safe(() => _queue?.Stop(), "Stop");

    private void OnUnqueueClick(object? sender, EventArgs e) => Safe(() =>
    {
        foreach (var file in SelectedFiles())
        {
            if (_open.TryGetValue(file, out var video))
                _queue?.Remove(video);
            UpdateRow(file);
        }
    }, "Take out of the queue");

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        foreach (var video in _open.Values)
            UpdateRow(video.Path);

        if (_queue is not null && _videos.SelectedItems.Count == 1 && _open.TryGetValue((string)_videos.SelectedItems[0].Tag!, out var shown))
            _editor.ShowTitle(_queue.JobOf(shown) is { Step: ScanStep.Gaps or ScanStep.Plates or ScanStep.Waiting } job ? job.Status : null);

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool any = _videos.SelectedItems.Count > 0;
        _scan.Enabled = _queue is not null && any;
        _scanAll.Enabled = _queue is not null && _files.Count > 0;
        _stop.Enabled = _queue is { IsBusy: true };
        _unqueue.Enabled = _queue is { Waiting.Count: > 0 } && any;
    }

    private void OnOptionChanged(object? sender, EventArgs e)
    {
        if (_loadingOptions)
            return;

        var gaps = new MotionSettings((double)_defThreshold.Value, (double)_defMinGap.Value, (double)_defKeep.Value);
        if (gaps.Validate().Count > 0)
            return;
        _options.GapThresholdPercent = gaps.ThresholdPercent;
        _options.MinGapSeconds = gaps.MinStillSeconds;
        _options.KeepAroundSeconds = gaps.PaddingSeconds;
        _options.GapsAgain = _gapsAgain.Checked;
        _options.PlatesAgain = _platesAgain.Checked;
        _options.Save();
    }

    private void OnTimerTick(object? sender, EventArgs e) => UpdateButtons();

    // ===================================================================== helpers

    private void Safe(Action action, string context)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            bool expected = ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException;
            if (!expected)
                ErrorReporter.Report(ex, context);
            Log($"ERROR: {context}: {ex.Message}");
            MessageBox.Show(this, ex.Message, context, MessageBoxButtons.OK, expected ? MessageBoxIcon.Warning : MessageBoxIcon.Error);
        }
    }

    private void Log(string message)
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Log(message)));
            return;
        }

        _log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }
}
