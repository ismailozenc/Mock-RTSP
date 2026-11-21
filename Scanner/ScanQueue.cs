namespace MockRtsp;

/// <summary>Where a video is in the scan queue.</summary>
internal enum ScanStep { Waiting, Gaps, Plates, Done, Failed, Stopped }

/// <summary>One video in the scan queue.</summary>
internal sealed class ScanJob
{
    public ScanJob(ScanVideo video, bool gapsAgain, bool platesAgain)
    {
        Video = video;
        GapsAgain = gapsAgain;
        PlatesAgain = platesAgain;
    }

    public ScanVideo Video { get; }

    /// <summary>Measure the motion again even when the video has motion data.</summary>
    public bool GapsAgain { get; }

    /// <summary>Read the plates again even when they were read.</summary>
    public bool PlatesAgain { get; }

    public ScanStep Step { get; set; } = ScanStep.Waiting;

    /// <summary>0 to 1 within the step.</summary>
    public double Progress { get; set; }

    public string Status { get; set; } = "waiting";
}

/// <summary>
/// Scans videos one after another. For each: the gaps first (a motion scan, when the video has no
/// motion data or "again" was asked; the graph grows while it runs), then the plates and
/// directions of its playing parts (Birel Plaka's models; each part's cars arrive as soon as the
/// part is read and prefill it). The videos stay editable all the time: results go into the
/// ScanVideo on the UI thread, like the user's edits. Runs on the UI thread; the slow work runs
/// in the background.
/// </summary>
internal sealed class ScanQueue : IDisposable
{
    private readonly Control _ui;
    private readonly string _ffmpeg;
    private readonly string _modelsFolder;
    private readonly Func<MotionSettings> _gapDefaults;
    private readonly List<ScanJob> _waiting = new();
    private readonly List<ScanJob> _finished = new();
    private CancellationTokenSource? _cancel;
    private PlateScanner? _scanner;
    private bool _running;

    /// <param name="gapDefaults">The gap settings for a video scanned for the first time.</param>
    public ScanQueue(Control ui, string ffmpegPath, string modelsFolder, Func<MotionSettings> gapDefaults)
    {
        _ui = ui;
        _ffmpeg = ffmpegPath;
        _modelsFolder = modelsFolder;
        _gapDefaults = gapDefaults;
    }

    public ScanJob? Current { get; private set; }

    public IReadOnlyList<ScanJob> Waiting => _waiting;

    public bool IsBusy => Current is not null || _waiting.Count > 0;

    /// <summary>Raised when a job starts, moves on, progresses or ends.</summary>
    public event EventHandler? StateChanged;

    /// <summary>A line for the log.</summary>
    public event EventHandler<string>? Message;

    /// <summary>The job of a video: running, waiting, or the last one that ended; null when never queued.</summary>
    public ScanJob? JobOf(ScanVideo video) =>
        (Current is { } c && c.Video == video ? c : null)
        ?? _waiting.FirstOrDefault(j => j.Video == video)
        ?? _finished.LastOrDefault(j => j.Video == video);

    /// <summary>Queues a video (nothing happens when it is queued or being scanned already).</summary>
    public void Add(ScanVideo video, bool gapsAgain, bool platesAgain)
    {
        if (!video.CanEdit)
            throw new InvalidOperationException($"{video.Name}: its file cannot be read ({video.LoadError}). Fix or delete the file, then open the folder again.");
        if (Current?.Video == video || _waiting.Any(j => j.Video == video))
            return;

        _finished.RemoveAll(j => j.Video == video);
        _waiting.Add(new ScanJob(video, gapsAgain, platesAgain));
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (!_running)
            RunAll();
    }

    /// <summary>Takes a waiting video out of the queue (the one being scanned goes on; Stop stops it).</summary>
    public void Remove(ScanVideo video)
    {
        if (_waiting.RemoveAll(j => j.Video == video) > 0)
            StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops the video being scanned and empties the queue. What was found so far is kept.</summary>
    public void Stop()
    {
        foreach (var job in _waiting)
        {
            job.Step = ScanStep.Stopped;
            job.Status = "not scanned (stopped)";
            _finished.Add(job);
        }

        _waiting.Clear();
        _cancel?.Cancel();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void RunAll()
    {
        _running = true;
        try
        {
            while (_waiting.Count > 0)
            {
                var job = _waiting[0];
                _waiting.RemoveAt(0);
                Current = job;
                using var cancel = _cancel = new CancellationTokenSource();
                try
                {
                    await RunAsync(job, cancel.Token);
                    job.Step = ScanStep.Done;
                    job.Progress = 1;
                    job.Status = Describe(job.Video);
                }
                catch (OperationCanceledException)
                {
                    job.Step = ScanStep.Stopped;
                    job.Status = "stopped; what was found so far is kept";
                    Say($"{job.Video.Name}: scan stopped; what was found so far is kept.");
                }
                catch (Exception ex)
                {
                    job.Step = ScanStep.Failed;
                    job.Status = "failed: " + ex.Message.Split('\n')[0];
                    ErrorReporter.Report(ex, $"Scanning {job.Video.Path}");
                    Say($"ERROR: {job.Video.Name}: the scan failed: {ex.Message}");
                }
                finally
                {
                    job.Video.ClearLiveMotion();
                    _cancel = null;
                    Current = null;
                    _finished.Add(job);
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    private async Task RunAsync(ScanJob job, CancellationToken cancel)
    {
        var video = job.Video;

        // 1. The gaps: measure the motion (5 times a second), then pick the still parts.
        if (job.GapsAgain || !video.Scanned)
        {
            job.Step = ScanStep.Gaps;
            job.Status = "finding gaps";
            StateChanged?.Invoke(this, EventArgs.Empty);
            Say($"{video.Name}: finding the gaps (measuring motion)...");

            using (var scan = MotionScan.Start(_ffmpeg, video.Path, video.Info))
            {
                while (!scan.IsComplete)
                {
                    await Task.Delay(250, cancel);
                    job.Progress = scan.Progress;
                    job.Status = $"finding gaps {scan.Progress * 100:0}%";
                    video.ShowLiveMotion(scan.SoFar);
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }

                if (scan.Result is not { } motion)
                    throw new InvalidOperationException("the motion scan failed: " + scan.Error);
                video.SetMotion(motion, video.MotionSettings ?? _gapDefaults());
            }
        }

        // 2. The plates and directions of every playing part, part by part.
        if (job.PlatesAgain || video.Reading is null)
        {
            var parts = video.Parts;
            var area = video.ReadArea;
            job.Step = ScanStep.Plates;
            job.Progress = 0;
            job.Status = "reading plates";
            StateChanged?.Invoke(this, EventArgs.Empty);
            Say($"{video.Name}: reading the plates of {parts.Count} playing part(s) in {area.Describe()}...");

            _scanner ??= await Task.Run(() => new PlateScanner(_ffmpeg, _modelsFolder, PlateScanSettings.BirelPlakaFactory), cancel);
            var scanner = _scanner;
            var settings = PlateScanSettings.BirelPlakaFactory;
            video.BeginReading(new PlateScan(DateTime.UtcNow, PlateScanSettings.DetectorFile, PlateScanSettings.OcrFile, settings.IntervalMs,
                area.DetectionText, area.PerspectiveText, new List<ScannedPart>(), new List<PlateCar>()));

            long shown = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await Task.Run(() => scanner.Scan(video.Path, video.Info, parts, area,
                    (share, message) =>
                    {
                        if (watch.ElapsedMilliseconds - Interlocked.Read(ref shown) < 200)
                            return;
                        Interlocked.Exchange(ref shown, watch.ElapsedMilliseconds);
                        Post(() =>
                        {
                            job.Progress = share;
                            job.Status = $"reading plates {share * 100:0}%: {message}";
                            StateChanged?.Invoke(this, EventArgs.Empty);
                        });
                    },
                    (part, cars) => Post(() =>
                    {
                        string? note = video.AddReadPart(part, cars);
                        if (note is not null)
                            Say($"{video.Name}: {note}.");
                    }),
                    cancel), cancel);
            }
            finally
            {
                video.EndReading();
            }

            Say($"{video.Name}: plates read in {watch.Elapsed.TotalSeconds:0} s: {video.Reading?.Cars.Count ?? 0} car(s).");
        }
    }

    /// <summary>For the list: what a video has, after its scan.</summary>
    public static string Describe(ScanVideo video) =>
        video.Reading is { } r ? $"done: {r.Cars.Count} car(s) read" : video.Scanned ? "done: gaps found" : "not scanned";

    private void Post(Action action)
    {
        if (_ui.IsDisposed || !_ui.IsHandleCreated)
            return;
        _ui.BeginInvoke(new Action(() =>
        {
            if (!_ui.IsDisposed)
                action();
        }));
    }

    private void Say(string message) => Message?.Invoke(this, message);

    public void Dispose()
    {
        _waiting.Clear();
        _cancel?.Cancel();
        if (Current is null)
            _scanner?.Dispose();        // else it is still in use on its thread; the app is closing anyway
    }
}
