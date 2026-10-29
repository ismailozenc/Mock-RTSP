using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MockRtsp;

/// <summary>One video in a camera's playlist: the file and its size, frame rate and length.</summary>
public sealed record PlaylistItem(string File, VideoInfo Info)
{
    public string Name => Path.GetFileName(File);
}

/// <summary>
/// A ground-truth plate went out on the stream: the frame at the plate's time was just handed
/// to the encoder. Utc is when; Format is what the camera sends, for working out where the
/// plate is in the streamed picture.
/// </summary>
public sealed record PlateStreamedEventArgs(DateTime Utc, PlaylistItem Video, PlateMark Plate, VideoFormat Format);

/// <summary>
/// A mock camera that plays a playlist of video files into MediaMTX like a live camera, with
/// pause, seek, speed and skip ranges per video.
///
/// It runs two FFmpeg processes with a pump thread between them:
///   decoder  reads one file from a position and outputs raw frames, scaled (letterboxed when the
///            shape differs) to the camera's format. Restarted to seek and for the next video.
///   pump     sends exactly one frame per frame interval to the encoder. Paused, it sends the
///            last frame again; at 2x speed it takes two source frames per output frame.
///   encoder  turns the frames into H.264 and publishes them. Runs for as long as the camera is on.
/// Because the encoder never stops, viewers stay connected through pause, seek and the change
/// from one video to the next, as they would with a real camera.
/// The camera's size and frame rate come from its settings and the first video of the playlist.
/// </summary>
public sealed class MockCamera : IDisposable
{
    private readonly object _gate = new();          // guards the running processes and _options
    private readonly MediaMtxServer _server;
    private readonly string _path;
    private readonly string _ffmpegPath;
    private StreamOptions _options;
    private Process? _encoder;
    private Thread? _pump;
    private CancellationTokenSource? _stop;
    private bool _disposed;

    // Player state, shared by the UI thread and the pump. Guarded by _control.
    private readonly object _control = new();
    private List<Entry> _playlist;
    private int _item;                              // playlist index of the frame being sent
    private double _positionSeconds;                // position in that video
    private Target? _seekTo;                        // a seek waiting for the pump
    private bool _paused;
    private bool _ended;                            // paused because a video that does not loop ended
    private double _speed = 1.0;
    private volatile bool _loop;                    // copy of _options.Loop, readable without _gate

    // Pump thread only: plates reached by the frame being sent, raised once it is written.
    private readonly List<(PlaylistItem Video, PlateMark Plate)> _plateHits = new();
    private Target? _lastShown;                     // the frame shown before, to find plates passed since
    private double _measuredFps;                    // frames actually written per second, over the last second

    // The last lines FFmpeg wrote, kept to explain why a camera stopped.
    private readonly Queue<string> _ffmpegOutput = new();
    private const int MaxOutputLines = 8;

    /// <summary>A place in the playlist.</summary>
    private readonly record struct Target(int Item, double Seconds);

    /// <summary>A playlist item with its skip ranges.</summary>
    private sealed class Entry
    {
        public Entry(PlaylistItem item, IReadOnlyList<SkipRange> skips)
        {
            Item = item;
            Skips = skips;
        }

        public PlaylistItem Item { get; }
        public IReadOnlyList<SkipRange> Skips { get; set; }
        public IReadOnlyList<PlateMark> Plates { get; set; } = Array.Empty<PlateMark>();
        public double Duration => Item.Info.DurationSeconds;
    }

    /// <summary>
    /// Raised when the camera stops by itself (FFmpeg failed, the encoder was refused, the file
    /// could not be read). Not raised for Stop, SimulateOutageAsync or Dispose.
    /// Raised on a background thread.
    /// </summary>
    public event EventHandler<int>? PublisherExited;

    /// <summary>
    /// Raised when a ground-truth plate goes out on the stream: the frame at the plate's time has
    /// just been written to the encoder. Not raised for plates in skipped parts, or passed over by
    /// a seek. Raised on the camera's own thread, so keep handlers short.
    /// </summary>
    public event EventHandler<PlateStreamedEventArgs>? PlateStreamed;

    /// <param name="path">Stream path, e.g. "Streaming/Channels/101" to match Hikvision URLs.</param>
    /// <param name="playlist">The videos to play, in order. At least one.</param>
    /// <param name="options">Video settings for this camera. Null uses the defaults.</param>
    public MockCamera(
        MediaMtxServer server,
        string path,
        IReadOnlyList<PlaylistItem> playlist,
        string ffmpegPath = "ffmpeg",
        StreamOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Stream path is required.", nameof(path));

        _options = (options ?? new StreamOptions()).Clone();

        var problems = _options.Validate();
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(options));

        _server = server;
        _path = path.Trim().TrimStart('/');
        _ffmpegPath = ffmpegPath;
        _loop = _options.Loop;
        _playlist = CheckPlaylist(playlist).Select(i => new Entry(i, Array.Empty<SkipRange>())).ToList();
    }

    /// <summary>Plays a single file. Reads its details with FFmpeg; an unplayable file fails here.</summary>
    public MockCamera(MediaMtxServer server, string path, string sourceFile, string ffmpegPath = "ffmpeg", StreamOptions? options = null)
        : this(server, path, new[] { new PlaylistItem(sourceFile, ProbeExisting(ffmpegPath, sourceFile)) }, ffmpegPath, options)
    {
    }

    private static VideoInfo ProbeExisting(string ffmpegPath, string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException("Source video not found.", file);

        return VideoInfo.Probe(ffmpegPath, file);
    }

    private static List<PlaylistItem> CheckPlaylist(IReadOnlyList<PlaylistItem> playlist)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        if (playlist.Count == 0)
            throw new ArgumentException("A camera needs at least one video.", nameof(playlist));

        foreach (var item in playlist)
        {
            if (!File.Exists(item.File))
                throw new FileNotFoundException("Source video not found.", item.File);
        }

        return playlist.ToList();
    }

    // ===================================================================== information

    /// <summary>The camera's URL without login details. Safe to show in the log.</summary>
    public string Url => _server.BuildUrl(_path);

    /// <summary>The URL a viewer uses, including the viewer login when one is set.</summary>
    public string ViewerUrl
    {
        get
        {
            lock (_gate)
            {
                return _server.BuildUrl(_path, _options.ViewerUser, _options.ViewerPassword);
            }
        }
    }

    /// <summary>A copy of the current video settings for this camera.</summary>
    public StreamOptions Options
    {
        get
        {
            lock (_gate)
            {
                return _options.Clone();
            }
        }
    }

    /// <summary>The substream's path (".../102" for ".../101"), or null when the substream is off.</summary>
    public string? SubstreamPath
    {
        get
        {
            lock (_gate)
            {
                return _options.Substream ? StreamOptions.SubstreamPathFor(_path) : null;
            }
        }
    }

    /// <summary>The substream's URL without login details, or null when it is off.</summary>
    public string? SubstreamUrl => SubstreamPath is { } sub ? _server.BuildUrl(sub) : null;

    /// <summary>The substream's URL for a viewer, with the viewer login, or null when it is off.</summary>
    public string? SubstreamViewerUrl
    {
        get
        {
            string? sub = SubstreamPath;
            if (sub is null)
                return null;

            lock (_gate)
            {
                return _server.BuildUrl(sub, _options.ViewerUser, _options.ViewerPassword);
            }
        }
    }

    /// <summary>The substream's size and frame rate.</summary>
    public VideoFormat SubstreamFormat
    {
        get
        {
            var main = Format;
            lock (_gate)
            {
                return _options.SubstreamFormat(main);
            }
        }
    }

    /// <summary>Frames per second actually sent over the last second; 0 when stopped.</summary>
    public double MeasuredFrameRate => IsOnline ? Volatile.Read(ref _measuredFps) : 0;

    /// <summary>The size and frame rate this camera sends: its settings applied to the first video.</summary>
    public VideoFormat Format
    {
        get
        {
            var first = Playlist[0].Info;
            lock (_gate)
            {
                return _options.OutputFormat(first);
            }
        }
    }

    /// <summary>The videos this camera plays, in order.</summary>
    public IReadOnlyList<PlaylistItem> Playlist
    {
        get
        {
            lock (_control)
            {
                return _playlist.Select(e => e.Item).ToList();
            }
        }
    }

    /// <summary>Which video of the playlist is playing, from 0.</summary>
    public int ItemIndex
    {
        get
        {
            lock (_control)
            {
                return _item;
            }
        }
    }

    /// <summary>The video playing now.</summary>
    public PlaylistItem Current
    {
        get
        {
            lock (_control)
            {
                return _playlist[_item].Item;
            }
        }
    }

    /// <summary>Size, frame rate and length of the video playing now.</summary>
    public VideoInfo Source => Current.Info;

    /// <summary>The length of the video playing now. Zero if FFmpeg did not report it.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(Source.DurationSeconds);

    /// <summary>Where in the video playing now the picture being sent comes from.</summary>
    public TimeSpan Position
    {
        get
        {
            lock (_control)
            {
                return TimeSpan.FromSeconds(_positionSeconds);
            }
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_control)
            {
                return _paused;
            }
        }
    }

    /// <summary>
    /// Sets the parts of a video to jump over while streaming, for every place it has in the
    /// playlist. Takes effect straight away; a range covering the current position makes the
    /// camera jump past it.
    /// </summary>
    public void SetSkipRanges(string file, IReadOnlyList<SkipRange> ranges)
    {
        lock (_control)
        {
            foreach (var entry in _playlist.Where(e => SameFile(e.Item.File, file)))
                entry.Skips = MockRtsp.SkipRanges.Normalize(ranges ?? Array.Empty<SkipRange>(), entry.Duration);

            // Sitting still in a range that was just added: move past it now.
            if (_seekTo is null && SameFile(_playlist[_item].Item.File, file) && Resolve(new Target(_item, _positionSeconds)) is { } next)
                SeekLocked(next);
        }
    }

    /// <summary>Sets the ground-truth plates of a video, for every place it has in the playlist.</summary>
    public void SetPlates(string file, IReadOnlyList<PlateMark> plates)
    {
        lock (_control)
        {
            foreach (var entry in _playlist.Where(e => SameFile(e.Item.File, file)))
                entry.Plates = PlateMarks.Normalize(plates ?? Array.Empty<PlateMark>(), entry.Duration);
        }
    }

    /// <summary>The skip ranges in use for a video of the playlist.</summary>
    public IReadOnlyList<SkipRange> SkipRangesOf(string file)
    {
        lock (_control)
        {
            return _playlist.FirstOrDefault(e => SameFile(e.Item.File, file))?.Skips ?? Array.Empty<SkipRange>();
        }
    }

    /// <summary>
    /// Replaces the playlist. Skips of videos that stay are kept; new ones start with none (set
    /// them with SetSkipRanges). The video playing carries on if it stays, otherwise the first
    /// video starts. The format does not change until the camera restarts.
    /// </summary>
    public void SetPlaylist(IReadOnlyList<PlaylistItem> playlist)
    {
        var items = CheckPlaylist(playlist);

        lock (_control)
        {
            var old = _playlist;
            var playing = old[_item].Item.File;

            _playlist = items
                .Select(i =>
                {
                    var before = old.FirstOrDefault(e => SameFile(e.Item.File, i.File));
                    return new Entry(i, before?.Skips ?? Array.Empty<SkipRange>()) { Plates = before?.Plates ?? Array.Empty<PlateMark>() };
                })
                .ToList();

            int index = _playlist.FindIndex(e => SameFile(e.Item.File, playing));
            if (index < 0)
            {
                SeekLocked(new Target(0, 0));
            }
            else if (index != _item)
            {
                // Same video, new place in the list: the decoder carries on.
                _item = index;
                if (_seekTo is { } pending)
                    _seekTo = pending with { Item = index };
            }
        }
    }

    /// <summary>Playback speed, 0.1 to 8. 1 is normal speed.</summary>
    public double Speed
    {
        get
        {
            lock (_control)
            {
                return _speed;
            }
        }
        set
        {
            lock (_control)
            {
                _speed = Math.Clamp(value, 0.1, 8.0);
            }
        }
    }

    /// <summary>The last lines FFmpeg printed, for showing why it stopped. Empty if it printed nothing.</summary>
    public string LastFfmpegOutput
    {
        get
        {
            lock (_ffmpegOutput)
            {
                return string.Join(Environment.NewLine, _ffmpegOutput);
            }
        }
    }

    public bool IsOnline
    {
        get
        {
            lock (_gate)
            {
                return _pump is { IsAlive: true } && _encoder is not null && IsProcessRunning(_encoder);
            }
        }
    }

    // ===================================================================== player controls

    /// <summary>Freezes the picture. Viewers stay connected and keep receiving the same frame.</summary>
    public void Pause()
    {
        lock (_control)
        {
            _paused = true;
        }
    }

    public void Resume()
    {
        lock (_control)
        {
            // At the end of a playlist that does not loop, play it again from the start.
            if (_ended)
                SeekLocked(new Target(0, 0));

            _paused = false;
        }
    }

    /// <summary>Jumps to a position in the video playing now. Works while paused, and while stopped (the camera starts there).</summary>
    public void SeekTo(TimeSpan position)
    {
        lock (_control)
        {
            SeekLocked(new Target(_item, position.TotalSeconds));
        }
    }

    /// <summary>Jumps forward (positive) or back (negative) from the current position, within the video playing now.</summary>
    public void SeekBy(TimeSpan offset)
    {
        lock (_control)
        {
            SeekLocked(new Target(_item, _positionSeconds + offset.TotalSeconds));
        }
    }

    /// <summary>Starts a video of the playlist from its beginning.</summary>
    public void PlayItem(int index)
    {
        lock (_control)
        {
            if (index < 0 || index >= _playlist.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "There is no such video in the playlist.");

            SeekLocked(new Target(index, 0));
        }
    }

    /// <summary>
    /// Back to the start of the video playing now, or, within the first 2 seconds of it, to the
    /// start of the video before it. Returns the video it went to.
    /// </summary>
    public int Rewind()
    {
        lock (_control)
        {
            int index = _positionSeconds < 2 && _item > 0 ? _item - 1 : _item;
            SeekLocked(new Target(index, 0));
            return index;
        }
    }

    /// <summary>Starts the next video of the playlist, or the first after the last. Returns its index.</summary>
    public int Next()
    {
        lock (_control)
        {
            int index = (_item + 1) % _playlist.Count;
            SeekLocked(new Target(index, 0));
            return index;
        }
    }

    /// <summary>Schedules a jump for the pump, and shows it as the position straight away. Caller holds _control.</summary>
    private void SeekLocked(Target target)
    {
        int item = Math.Clamp(target.Item, 0, _playlist.Count - 1);
        double duration = _playlist[item].Duration;
        double seconds = Math.Max(0, target.Seconds);

        if (duration > 0)
            seconds = Math.Min(seconds, Math.Max(0, duration - 0.25));

        _seekTo = new Target(item, seconds);
        _item = item;
        _positionSeconds = seconds;
        _ended = false;
    }

    // ===================================================================== settings

    /// <summary>
    /// Replaces the video settings. A running camera restarts so the change takes effect,
    /// which disconnects viewers, as a real camera does when its resolution changes.
    /// Position, pause and speed are kept.
    /// </summary>
    public void ApplyOptions(StreamOptions options)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);

        var copy = options.Clone();
        var problems = copy.Validate();
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(options));

        bool wasRunning = IsOnline;

        Stop();

        lock (_gate)
        {
            _options = copy;
            _loop = copy.Loop;
        }

        if (wasRunning)
            Start();
    }

    /// <summary>
    /// Waits until a viewer can actually play the stream. Returns false if the camera stops or
    /// the stream is not up within the timeout.
    /// </summary>
    public async Task<bool> WaitUntilLiveAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (_disposed || !IsOnline)
                return false;

            string user, password;
            lock (_gate)
            {
                user = _options.ViewerUser;
                password = _options.ViewerPassword;
            }

            if (await _server.IsPathReadyAsync(_path, user, password, ct))
                return true;

            await Task.Delay(250, ct);
        }

        return false;
    }

    // ===================================================================== start and stop

    public void Start()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_pump is { IsAlive: true })
                return;

            KillQuietly(_encoder);
            _encoder = null;

            lock (_ffmpegOutput)
            {
                _ffmpegOutput.Clear();
            }

            var format = Format;
            var encoder = StartEncoder(format);
            var stop = new CancellationTokenSource();

            var pump = new Thread(() => Pump(encoder, format, stop.Token))
            {
                IsBackground = true,
                Name = "MockCamera " + _path,
                Priority = ThreadPriority.AboveNormal
            };

            _encoder = encoder;
            _stop = stop;
            _pump = pump;
            pump.Start();
        }
    }

    /// <summary>Stops the camera. MediaMTX drops the stream, just as a camera going offline would.</summary>
    public void Stop()
    {
        Thread? pump;
        Process? encoder;
        CancellationTokenSource? stop;

        lock (_gate)
        {
            // Clear the fields first, so the pump treats what follows as a requested stop.
            pump = _pump;
            encoder = _encoder;
            stop = _stop;
            _pump = null;
            _encoder = null;
            _stop = null;
        }

        stop?.Cancel();
        KillQuietly(encoder);          // also unblocks a pump that is writing a frame

        if (pump is not null && pump != Thread.CurrentThread && !pump.Join(5000))
            ErrorReporter.Report(new TimeoutException("The camera thread did not stop in time."), $"Stopping {_path}");

        stop?.Dispose();
    }

    /// <summary>
    /// Simulates a camera dropping off the network for a period, then coming back. Like a real
    /// camera, it comes back showing "now": unless paused, the video has moved on by the downtime.
    /// </summary>
    public async Task SimulateOutageAsync(TimeSpan downtime, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (downtime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(downtime), "Downtime must be positive.");

        Stop();
        var stoppedAt = Stopwatch.StartNew();

        try
        {
            await Task.Delay(downtime, ct);
        }
        finally
        {
            // Bring the camera back even if the wait was cancelled, unless it was disposed meanwhile.
            if (!_disposed)
            {
                MoveOnBy(stoppedAt.Elapsed);
                Start();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    // ===================================================================== the pump

    private void Pump(Process encoder, VideoFormat format, CancellationToken stop)
    {
        using var timer = HighResolutionTimer.Begin();
        Decoder? decoder = null;

        try
        {
            var output = encoder.StandardInput.BaseStream;
            var frame = new byte[format.FrameBytes];        // the frame being sent
            var next = new byte[format.FrameBytes];

            // Start from black (in yuv420p black is Y = 0 with U and V at 128), not green.
            int lumaBytes = format.Width * format.Height;
            Array.Fill(frame, (byte)128, lumaBytes, format.FrameBytes - lumaBytes);

            Target start;
            lock (_control)
            {
                start = new Target(_item, _positionSeconds);
                _seekTo = null;                             // the start position already covers it
                _lastShown = null;
            }

            _plateHits.Clear();
            var rateClock = Stopwatch.StartNew();
            int rateFrames = 0;

            decoder = StartDecoder(start, format);
            if (ReadFrame(ref decoder, format, next))
                Show(ref frame, ref next, decoder!, format);

            var clock = Stopwatch.StartNew();
            long sent = 0;
            double owed = 0;                                // source frames due, for speeds other than 1x

            while (!stop.IsCancellationRequested)
            {
                Target? seek;
                bool paused;
                double speed;
                lock (_control)
                {
                    seek = _seekTo;
                    _seekTo = null;
                    paused = _paused;
                    speed = _speed;
                }

                if (seek is { } target)
                {
                    // Only the decoder restarts. The encoder, and every viewer, carries on.
                    decoder?.Dispose();
                    decoder = StartDecoder(target, format);
                    owed = 0;

                    // Show the new position straight away, also while paused.
                    if (ReadFrame(ref decoder, format, next))
                        Show(ref frame, ref next, decoder!, format);
                }
                else if (!paused)
                {
                    owed += speed;
                    while (owed >= 1)
                    {
                        owed -= 1;
                        if (ReadFrame(ref decoder, format, next))
                        {
                            if (!Show(ref frame, ref next, decoder!, format))
                            {
                                owed = 0;               // a skip range starts here; the jump comes next
                                break;
                            }
                        }
                        else
                        {
                            // End of a playlist that does not loop: hold the last frame.
                            lock (_control)
                            {
                                _paused = true;
                                _ended = true;
                            }
                            owed = 0;
                            break;
                        }
                    }
                }

                output.Write(frame, 0, frame.Length);
                sent++;

                if (_plateHits.Count > 0)
                    RaisePlates(format);

                rateFrames++;
                if (rateClock.ElapsedMilliseconds >= 1000)
                {
                    Volatile.Write(ref _measuredFps, rateFrames * 1000.0 / rateClock.ElapsedMilliseconds);
                    rateFrames = 0;
                    rateClock.Restart();
                }

                // Frame n is due n frame-intervals after the start. If the computer stalled for
                // over a second, start a fresh schedule instead of bursting to catch up.
                double due = sent * 1000.0 / format.FrameRate;
                if (clock.Elapsed.TotalMilliseconds - due > 1000)
                {
                    clock.Restart();
                    sent = 0;
                }
                else
                {
                    WaitUntil(clock, due, stop);
                }
            }
        }
        catch (Exception ex) when (!stop.IsCancellationRequested)
        {
            OnPumpFailed(encoder, ex);
        }
        catch (Exception)
        {
            // Stop() killed the encoder while a frame was being written. Expected.
        }
        finally
        {
            decoder?.Dispose();
            Volatile.Write(ref _measuredFps, 0);
        }
    }

    /// <summary>Tells listeners about plates whose frame was just written. Pump thread.</summary>
    private void RaisePlates(VideoFormat format)
    {
        var now = DateTime.UtcNow;
        foreach (var (video, plate) in _plateHits)
        {
            try
            {
                PlateStreamed?.Invoke(this, new PlateStreamedEventArgs(now, video, plate, format));
            }
            catch (Exception ex)
            {
                ErrorReporter.Report(ex, $"Camera {_path} plate listener");
            }
        }

        _plateHits.Clear();
    }

    /// <summary>
    /// Reads the next frame into the buffer. At the end of a video it goes on with the next one,
    /// and after the last with the first when looping; otherwise returns false.
    /// </summary>
    private bool ReadFrame(ref Decoder? decoder, VideoFormat format, byte[] into)
    {
        // A video that gives no frames at all is passed over, but not forever.
        for (int attempt = 0; attempt <= PlaylistCount + 1; attempt++)
        {
            if (decoder is not null && decoder.TryRead(into))
                return true;

            // The decoder ended: end of the file, or it failed.
            bool producedNothing = decoder is null || decoder.Frames == 0;
            var at = decoder?.At ?? new Target(0, 0);
            decoder?.Dispose();
            decoder = null;

            if (producedNothing && at.Seconds <= 0)
            {
                if (PlaylistCount == 1)
                    throw new InvalidOperationException("FFmpeg could not decode the video file.");

                Remember("camera", $"FFmpeg could not decode {NameOf(at.Item)}; going on with the next video.");
            }

            if (NextAfter(at.Item) is not int next)
                return false;

            lock (_control)
            {
                // A seek picked meanwhile keeps its place; the pump starts it next.
                if (_seekTo is null)
                {
                    _item = next;
                    _positionSeconds = 0;
                }
            }

            decoder = StartDecoder(new Target(next, 0), format);
        }

        throw new InvalidOperationException("FFmpeg could not decode the videos of this camera. " + LastFfmpegOutput);
    }

    /// <summary>The video after this one, wrapping when looping, or null at the end of a playlist that does not loop.</summary>
    private int? NextAfter(int item)
    {
        lock (_control)
        {
            return NextAfterLocked(item);
        }
    }

    private int PlaylistCount
    {
        get
        {
            lock (_control)
            {
                return _playlist.Count;
            }
        }
    }

    private string NameOf(int item)
    {
        lock (_control)
        {
            return item < _playlist.Count ? _playlist[item].Item.Name : "a video";
        }
    }

    /// <summary>
    /// Where to go from a position if it lies in a skip range: past it, to the next video when
    /// the rest of this one is skipped. Null when the position plays. Caller holds _control.
    /// </summary>
    private Target? Resolve(Target at)
    {
        if (at.Item >= _playlist.Count)
            return null;

        var entry = _playlist[at.Item];
        var (decision, target) = MockRtsp.SkipRanges.Resolve(entry.Skips, at.Seconds, entry.Duration, loop: false);

        return decision switch
        {
            MockRtsp.SkipRanges.Decision.JumpTo => new Target(at.Item, target),
            MockRtsp.SkipRanges.Decision.EndReached => NextAfterLocked(at.Item) is int next ? new Target(next, 0) : new Target(-1, 0),
            _ => null
        };
    }

    private int? NextAfterLocked(int item)
    {
        if (item + 1 < _playlist.Count)
            return item + 1;

        return _loop ? 0 : null;
    }

    /// <summary>
    /// Makes the frame just read the one being sent, unless it lies in a skip range. Then it is
    /// never shown: a jump past the range is scheduled instead, so viewers see a clean cut.
    /// Returns false when the frame was skipped.
    /// </summary>
    private bool Show(ref byte[] frame, ref byte[] next, Decoder decoder, VideoFormat format)
    {
        var at = new Target(decoder.At.Item, decoder.At.Seconds + (decoder.Frames - 1) / (double)format.FrameRate);

        lock (_control)
        {
            // A seek requested meanwhile wins; this frame is from before it.
            if (_seekTo is not null)
                return false;

            if (Resolve(at) is { } jump)
            {
                if (jump.Item < 0)
                {
                    _paused = true;             // the rest of the playlist is skipped: hold the last frame
                    _ended = true;
                    return false;
                }

                SeekLocked(jump);
                return false;
            }

            _item = at.Item;
            _positionSeconds = at.Seconds;
            CollectPlates(at, format);
        }

        (frame, next) = (next, frame);
        return true;
    }

    /// <summary>
    /// Notes the plates this frame reaches: those after the frame shown before (when playing on
    /// from it) up to this one. After a jump, only a plate on this very frame counts. Caller holds _control.
    /// </summary>
    private void CollectPlates(Target at, VideoFormat format)
    {
        var entry = _playlist[at.Item];
        double from = _lastShown is { } last && last.Item == at.Item && at.Seconds >= last.Seconds && at.Seconds - last.Seconds < 2
            ? last.Seconds
            : at.Seconds - 1.0 / format.FrameRate;
        _lastShown = at;

        foreach (var plate in entry.Plates)
        {
            if (plate.Time > from && plate.Time <= at.Seconds)
                _plateHits.Add((entry.Item, plate));
        }
    }

    /// <summary>After an outage: moves on by the time the camera was down, through the playlist.</summary>
    private void MoveOnBy(TimeSpan elapsed)
    {
        bool loop = _loop;

        lock (_control)
        {
            if (_paused)
                return;

            int item = _item;
            double position = _positionSeconds + elapsed.TotalSeconds * _speed;

            // Whole videos passed meanwhile; stop counting after a few rounds of a short playlist.
            for (int guard = 0; guard < 10_000; guard++)
            {
                double duration = _playlist[item].Duration;
                if (duration <= 0 || position < duration)
                    break;

                position -= duration;
                if (item + 1 < _playlist.Count)
                {
                    item++;
                }
                else if (loop)
                {
                    item = 0;
                }
                else
                {
                    position = Math.Max(0, duration - 0.25);
                    break;
                }
            }

            _item = item;
            _positionSeconds = position;
        }
    }

    private static bool SameFile(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static void WaitUntil(Stopwatch clock, double dueMilliseconds, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            double remaining = dueMilliseconds - clock.Elapsed.TotalMilliseconds;
            if (remaining <= 0)
                return;

            if (remaining > 2)
                Thread.Sleep((int)(remaining - 1));
            else
                Thread.SpinWait(100);
        }
    }

    private void OnPumpFailed(Process encoder, Exception ex)
    {
        try
        {
            int exitCode = -1;

            if (ex is IOException)
            {
                // The encoder went away (for example MediaMTX refused the stream). Let its last
                // output arrive, because that says why.
                try
                {
                    if (encoder.WaitForExit(2000))
                    {
                        encoder.WaitForExit();
                        exitCode = encoder.ExitCode;
                    }
                }
                catch (Exception waitError) when (waitError is InvalidOperationException or Win32Exception)
                {
                }
            }
            else
            {
                Remember("camera", ex.Message);
            }

            lock (_gate)
            {
                if (!ReferenceEquals(_encoder, encoder))
                    return;                                 // stopped or replaced meanwhile

                _encoder = null;
                _pump = null;
                _stop = null;
            }

            KillQuietly(encoder);
            PublisherExited?.Invoke(this, exitCode);
        }
        catch (Exception inner)
        {
            ErrorReporter.Report(inner, $"Camera {_path} stop handler");
        }
    }

    // ===================================================================== FFmpeg processes

    private Process StartEncoder(VideoFormat format)
    {
        // FFmpeg must send the publisher login, or MediaMTX refuses the stream.
        string publishUrl = _server.BuildUrl(_path, _options.PublisherUser, _options.PublisherPassword);

        var psi = new ProcessStartInfo(_ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };

        string? substreamUrl = _options.Substream
            ? _server.BuildUrl(StreamOptions.SubstreamPathFor(_path), _options.PublisherUser, _options.PublisherPassword)
            : null;

        foreach (var arg in _options.EncoderArguments(format, publishUrl, substreamUrl))
            psi.ArgumentList.Add(arg);

        var process = StartProcess(psi);
        process.ErrorDataReceived += (_, e) => Remember("encoder", e.Data);
        process.BeginErrorReadLine();
        return process;
    }

    private Decoder StartDecoder(Target at, VideoFormat format)
    {
        string file;
        lock (_control)
        {
            file = _playlist[Math.Clamp(at.Item, 0, _playlist.Count - 1)].Item.File;
        }

        var psi = new ProcessStartInfo(_ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in StreamOptions.DecoderArguments(file, at.Seconds, format))
            psi.ArgumentList.Add(arg);

        var process = StartProcess(psi);
        process.ErrorDataReceived += (_, e) => Remember("decoder", e.Data);
        process.BeginErrorReadLine();
        return new Decoder(process, at);
    }

    private Process StartProcess(ProcessStartInfo psi)
    {
        try
        {
            return Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start FFmpeg ('{_ffmpegPath}'). Check that ffmpeg.exe is in the ffmpeg folder.", ex);
        }
    }

    private void Remember(string source, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (_ffmpegOutput)
        {
            _ffmpegOutput.Enqueue($"{source}: {line.Trim()}");
            while (_ffmpegOutput.Count > MaxOutputLines)
                _ffmpegOutput.Dequeue();
        }
    }

    private static bool IsProcessRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private static void KillQuietly(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already gone.
        }
        finally
        {
            process.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockCamera));
    }

    /// <summary>One decoder process, reading one video of the playlist from a position.</summary>
    private sealed class Decoder : IDisposable
    {
        private readonly Process _process;
        private readonly Stream _output;
        private bool _disposed;

        public Decoder(Process process, Target at)
        {
            _process = process;
            _output = process.StandardOutput.BaseStream;
            At = at;
        }

        /// <summary>The video and the position it started reading from.</summary>
        public Target At { get; }

        /// <summary>Frames read so far.</summary>
        public int Frames { get; private set; }

        /// <summary>Reads one whole frame. False at the end of the output.</summary>
        public bool TryRead(byte[] buffer)
        {
            if (_disposed)
                return false;

            int read = _output.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            if (read < buffer.Length)
                return false;

            Frames++;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            KillQuietly(_process);
        }
    }

    /// <summary>
    /// Windows wakes sleeping threads every 15.6 ms by default, too coarse to send frames on
    /// time. This asks for 1 ms while a camera runs.
    /// </summary>
    private sealed class HighResolutionTimer : IDisposable
    {
        private bool _active;

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint milliseconds);

        public static HighResolutionTimer Begin()
        {
            var timer = new HighResolutionTimer();

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    timer._active = timeBeginPeriod(1) == 0;
                }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
                {
                }
            }

            return timer;
        }

        public void Dispose()
        {
            if (!_active) return;
            _active = false;
            timeEndPeriod(1);
        }
    }
}
