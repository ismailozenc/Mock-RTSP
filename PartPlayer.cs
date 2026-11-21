using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace MockRtsp;

/// <summary>
/// Plays one part of a video file inside the app, for labelling. FFmpeg decodes the part to raw
/// pictures and the control shows them at the video's own pace, looping by default. Independent of
/// any camera: it reads the file itself.
///   Play/Pause, one frame back or forward, back to the start of the part, speed, loop
///   Click or drag the bar under the picture: jump within the part
///   Mouse wheel: zoom around the mouse (to read a plate); drag: move the zoomed picture;
///   double-click: whole picture again
///   Keys (click the picture first): Space play/pause, Left/Right one frame back/forward
/// </summary>
public sealed class PartPlayer : Control
{
    private static readonly double[] Speeds = { 0.25, 0.5, 1.0, 2.0 };
    private const int MaxDecodeWidth = 1600;    // enough to read plates; keeps the pipe and drawing light
    private const int ProgressHeight = 14;

    private readonly FlowLayoutPanel _bar = new();
    private readonly Button _playPause = new();
    private readonly Button _frameBack = new();
    private readonly Button _frameForward = new();
    private readonly Button _toStart = new();
    private readonly ComboBox _speed = new();
    private readonly CheckBox _loop = new();
    private readonly Label _time = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private string? _ffmpeg;
    private string? _file;
    private VideoInfo? _info;
    private double _start;
    private double _end;
    private int _width;
    private int _height;

    private Decoder? _decoder;
    private Bitmap? _picture;
    private double? _pictureTime;
    private bool _paused;
    private double _nextDue;                // clock seconds at which the next picture is due
    private string? _message;               // shown instead of a picture: no FFmpeg, or what went wrong

    private float _zoom = 1;
    private PointF _focus = new(0.5f, 0.5f);    // the picture point (0 to 1) at the centre of the view
    private Point? _panFrom;
    private bool _seekDragging;

    /// <summary>Raised when the picture shown changes (another moment of the video).</summary>
    public event EventHandler? PositionChanged;

    public PartPlayer()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        BackColor = Color.Black;
        ForeColor = Color.Gainsboro;

        _playPause.Text = "Pause";
        _frameBack.Text = "< Frame";
        _frameForward.Text = "Frame >";
        _toStart.Text = "|< Start";
        foreach (var button in new[] { _playPause, _frameBack, _frameForward, _toStart })
        {
            button.AutoSize = true;
            button.UseVisualStyleBackColor = true;
            button.TabStop = false;
        }

        _speed.DropDownStyle = ComboBoxStyle.DropDownList;
        _speed.Width = 70;
        _speed.Margin = new Padding(10, 5, 3, 3);
        _speed.Items.AddRange(Speeds.Select(s => (object)(s.ToString("0.##", CultureInfo.InvariantCulture) + "x")).ToArray());
        _speed.SelectedIndex = Array.IndexOf(Speeds, 1.0);
        _speed.TabStop = false;

        _loop.Text = "Loop";
        _loop.Checked = true;
        _loop.AutoSize = true;
        _loop.Margin = new Padding(10, 8, 3, 3);
        _loop.ForeColor = Color.Gainsboro;
        _loop.TabStop = false;

        _time.AutoSize = true;
        _time.Margin = new Padding(10, 9, 3, 3);
        _time.ForeColor = Color.Gainsboro;

        _bar.Dock = DockStyle.Bottom;
        _bar.AutoSize = true;
        _bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _bar.BackColor = Color.FromArgb(36, 36, 40);
        _bar.Padding = new Padding(2);
        _bar.WrapContents = true;
        _bar.Controls.AddRange(new Control[] { _playPause, _frameBack, _frameForward, _toStart, _speed, _loop, _time });
        Controls.Add(_bar);

        _playPause.Click += (_, _) => TogglePause();
        _frameBack.Click += (_, _) => StepFrame(-1);
        _frameForward.Click += (_, _) => StepFrame(+1);
        _toStart.Click += (_, _) => SeekTo(_start, keepPaused: _paused);

        _timer.Interval = 5;
        _timer.Tick += OnTick;
        UpdateButtons();
    }

    // ===================================================================== what to play

    /// <summary>The video to play from; null ffmpegPath shows a note instead of a picture.</summary>
    public void SetVideo(string? ffmpegPath, string file, VideoInfo info)
    {
        StopDecoder();
        _ffmpeg = ffmpegPath;
        _file = file;
        _info = info;

        // Decode at the picture's own size up to MaxDecodeWidth, with a width the bitmap rows fit exactly.
        int width = Math.Max(16, Math.Min(info.Width > 0 ? info.Width : 960, MaxDecodeWidth)) / 4 * 4;
        int height = info.Width > 0 && info.Height > 0 ? (int)Math.Round(width * (double)info.Height / info.Width) : width * 9 / 16;
        _width = width;
        _height = Math.Max(2, height / 2 * 2);

        _message = ffmpegPath is null || !File.Exists(ffmpegPath)
            ? "The video cannot be shown here: ffmpeg.exe was not found (see Requirements in the README)."
            : null;
        Invalidate();
    }

    /// <summary>Plays a part of the video from its start, looping if Loop is ticked.</summary>
    public void PlayPart(double startSeconds, double endSeconds)
    {
        _start = Math.Max(0, startSeconds);
        _end = Math.Max(_start + 0.05, endSeconds);
        _paused = false;
        ResetZoom();
        SeekTo(_start, keepPaused: false);
    }

    /// <summary>Shows a moment of the part, paused (for example the time of a labelled vehicle).</summary>
    public void ShowMoment(double seconds) => SeekTo(Clamp(seconds, _start, _end), keepPaused: true);

    /// <summary>The time in the video of the picture shown, or null when there is none.</summary>
    public double? Position => _pictureTime;

    public void Stop()
    {
        StopDecoder();
        _timer.Stop();
    }

    // ===================================================================== playing

    private double FrameRate => _info is { FrameRate: > 0 } info ? info.FrameRate : 25;

    private double Speed => Speeds[Math.Max(0, _speed.SelectedIndex)];

    private void TogglePause()
    {
        _paused = !_paused;
        _nextDue = _clock.Elapsed.TotalSeconds;
        if (!_paused && _decoder is null)
            SeekTo(_pictureTime ?? _start, keepPaused: false);
        UpdateButtons();
        Focus();
    }

    /// <summary>One picture forward (the next one decoded) or back (decoding again from just before).</summary>
    private void StepFrame(int direction)
    {
        _paused = true;
        double now = _pictureTime ?? _start;

        if (direction > 0 && _decoder is not null && _decoder.Frames.TryTake(out var frame, 1500))
            Show(frame);
        else if (direction > 0)
            SeekTo(Math.Min(_end, now + 1 / FrameRate), keepPaused: true);
        else
            SeekTo(Math.Max(_start, now - 1 / FrameRate), keepPaused: true);

        UpdateButtons();
        Focus();
    }

    /// <summary>Starts decoding at a moment; paused, it waits for that picture and shows it.</summary>
    private void SeekTo(double seconds, bool keepPaused)
    {
        _paused = keepPaused;
        StopDecoder();

        if (_file is null || _ffmpeg is null || !File.Exists(_ffmpeg))
        {
            Invalidate();
            return;
        }

        seconds = Clamp(seconds, _start, Math.Max(_start, _end - 0.5 / FrameRate));
        try
        {
            _decoder = Decoder.Start(_ffmpeg, _file, seconds, _end, _width, _height, FrameRate);
            _message = null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _message = "The video cannot be shown: " + ex.Message;
            Invalidate();
            return;
        }

        // Show the first picture straight away, so a jump is seen even while paused.
        if (_decoder.Frames.TryTake(out var first, 3000))
            Show(first);
        else if (_decoder.Error is { } error)
            _message = "FFmpeg could not decode this part: " + error;

        _nextDue = _clock.Elapsed.TotalSeconds + 1 / FrameRate / Speed;
        _timer.Start();
        UpdateButtons();
        Invalidate();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_paused || _decoder is null)
            return;

        double now = _clock.Elapsed.TotalSeconds;
        if (now < _nextDue)
            return;

        if (_decoder.Frames.TryTake(out var frame))
        {
            Show(frame);
            double interval = 1 / FrameRate / Speed;
            _nextDue = now - _nextDue > 0.25 ? now + interval : _nextDue + interval;   // behind: catch up, don't race
        }
        else if (_decoder.Frames.IsCompleted)
        {
            // End of the part.
            if (_decoder.Error is { } error && _pictureTime is null)
            {
                _message = "FFmpeg could not decode this part: " + error;
                StopDecoder();
                Invalidate();
            }
            else if (_loop.Checked)
            {
                SeekTo(_start, keepPaused: false);
            }
            else
            {
                _paused = true;
                StopDecoder();
                UpdateButtons();
            }
        }
    }

    /// <summary>Puts a decoded picture on screen.</summary>
    private void Show(Frame frame)
    {
        if (_picture is null || _picture.Width != _width || _picture.Height != _height)
        {
            _picture?.Dispose();
            _picture = new Bitmap(_width, _height, PixelFormat.Format24bppRgb);
        }

        var data = _picture.LockBits(new Rectangle(0, 0, _width, _height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int row = _width * 3;
            for (int y = 0; y < _height; y++)
                Marshal.Copy(frame.Pixels, y * row, data.Scan0 + y * data.Stride, row);
        }
        finally
        {
            _picture.UnlockBits(data);
        }

        _pictureTime = frame.Time;
        _time.Text = $"{PlateMarks.FormatTime(frame.Time)}   (part {PlateMarks.FormatTime(_start)} to {PlateMarks.FormatTime(_end)})";
        Invalidate(VideoArea);
        Invalidate(ProgressArea);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateButtons()
    {
        _playPause.Text = _paused ? "Play" : "Pause";
        bool usable = _message is null && _file is not null;
        _playPause.Enabled = _frameBack.Enabled = _frameForward.Enabled = _toStart.Enabled = usable;
    }

    private void StopDecoder()
    {
        _decoder?.Dispose();
        _decoder = null;
    }

    // ===================================================================== drawing

    private Rectangle VideoArea => new(0, 0, ClientSize.Width, Math.Max(1, ClientSize.Height - _bar.Height - ProgressHeight));

    private Rectangle ProgressArea => new(0, VideoArea.Bottom, ClientSize.Width, ProgressHeight);

    /// <summary>Where the whole picture goes, letterboxed in the video area.</summary>
    private RectangleF FitRect()
    {
        var area = VideoArea;
        if (_width <= 0 || _height <= 0)
            return area;

        float scale = Math.Min(area.Width / (float)_width, area.Height / (float)_height);
        float w = _width * scale, h = _height * scale;
        return new RectangleF(area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2, w, h);
    }

    /// <summary>The part of the picture shown at the current zoom, in picture pixels.</summary>
    private RectangleF SourceRect()
    {
        float w = _width / _zoom, h = _height / _zoom;
        float x = Clamp(_focus.X * _width - w / 2, 0, _width - w);
        float y = Clamp(_focus.Y * _height - h / 2, 0, _height - h);
        return new RectangleF(x, y, w, h);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            var area = VideoArea;

            if (_message is not null || _picture is null)
            {
                string text = _message ?? (_file is null ? "Select a playing part to see it here." : "Loading the video...");
                TextRenderer.DrawText(g, text, Font, area, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            else
            {
                g.InterpolationMode = _zoom > 1.01 ? InterpolationMode.HighQualityBicubic : InterpolationMode.Bilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_picture, FitRect(), SourceRect(), GraphicsUnit.Pixel);

                if (_zoom > 1.01)
                {
                    string zoom = $"zoom {_zoom:0.#}x  (double-click: whole picture)";
                    var size = TextRenderer.MeasureText(zoom, Font);
                    var box = new Rectangle(area.Left + 6, area.Top + 6, size.Width + 6, size.Height + 2);
                    using var back = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
                    g.FillRectangle(back, box);
                    TextRenderer.DrawText(g, zoom, Font, box, Color.Khaki, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            DrawProgress(g);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Drawing the part player");
        }
    }

    /// <summary>The bar under the picture: the whole part, with the moment shown.</summary>
    private void DrawProgress(Graphics g)
    {
        var bar = ProgressArea;
        using (var back = new SolidBrush(Color.FromArgb(28, 28, 32)))
            g.FillRectangle(back, bar);

        if (_end <= _start)
            return;

        var track = Rectangle.Inflate(bar, -6, -4);
        using (var fill = new SolidBrush(Color.FromArgb(46, 160, 67)))
            g.FillRectangle(fill, track);

        if (_pictureTime is double t)
        {
            int x = track.Left + (int)Math.Round((t - _start) / (_end - _start) * track.Width);
            using var pen = new Pen(Color.FromArgb(235, 64, 52), 3);
            g.DrawLine(pen, x, bar.Top + 1, x, bar.Bottom - 1);
        }
    }

    // ===================================================================== mouse and keys

    private double TimeAtProgress(int x)
    {
        var track = Rectangle.Inflate(ProgressArea, -6, -4);
        return _start + Clamp((x - track.Left) / (double)Math.Max(1, track.Width), 0, 1) * (_end - _start);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left)
            return;

        if (ProgressArea.Contains(e.Location) && _file is not null)
        {
            _seekDragging = true;
            Capture = true;
        }
        else if (VideoArea.Contains(e.Location) && _zoom > 1.01)
        {
            _panFrom = e.Location;
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_panFrom is Point from)
        {
            var fit = FitRect();
            _focus = new PointF(
                Clamp(_focus.X - (e.X - from.X) / fit.Width / _zoom, 0, 1),
                Clamp(_focus.Y - (e.Y - from.Y) / fit.Height / _zoom, 0, 1));
            _panFrom = e.Location;
            Invalidate(VideoArea);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_seekDragging)
        {
            _seekDragging = false;
            Capture = false;
            SeekTo(TimeAtProgress(e.X), keepPaused: _paused);
        }

        _panFrom = null;
        Cursor = Cursors.Default;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (VideoArea.Contains(e.Location))
        {
            ResetZoom();
            Invalidate(VideoArea);
        }
    }

    /// <summary>Zooms around the point under the mouse, up to 8x.</summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var fit = FitRect();
        if (_picture is null || !fit.Contains(e.Location))
            return;

        var src = SourceRect();
        var under = new PointF(
            (src.X + (e.X - fit.X) / fit.Width * src.Width) / _width,
            (src.Y + (e.Y - fit.Y) / fit.Height * src.Height) / _height);

        float zoom = Clamp(_zoom * (float)Math.Pow(1.25, e.Delta / 120.0), 1f, 8f);
        // Keep the point under the mouse where it is.
        float fx = (e.X - fit.X) / fit.Width, fy = (e.Y - fit.Y) / fit.Height;
        _focus = new PointF(under.X + (0.5f - fx) / zoom, under.Y + (0.5f - fy) / zoom);
        _zoom = zoom;
        if (_zoom <= 1.01)
            ResetZoom();
        Invalidate(VideoArea);
    }

    private void ResetZoom()
    {
        _zoom = 1;
        _focus = new PointF(0.5f, 0.5f);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Space:
                TogglePause();
                e.Handled = true;
                break;
            case Keys.Left:
                StepFrame(-1);
                e.Handled = true;
                break;
            case Keys.Right:
                StepFrame(+1);
                e.Handled = true;
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            StopDecoder();
            _picture?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;

    private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    // ===================================================================== decoding

    private sealed record Frame(byte[] Pixels, double Time);

    /// <summary>
    /// One FFmpeg pass over a part: raw BGR pictures from a moment to the end of the part, read on
    /// a background thread into a small queue (so FFmpeg waits while the player is paused).
    /// </summary>
    private sealed class Decoder : IDisposable
    {
        private readonly Process _process;
        private readonly Thread _reader;
        private readonly List<string> _errors = new();
        private volatile bool _stopping;

        private Decoder(Process process, Thread reader)
        {
            _process = process;
            _reader = reader;
        }

        public BlockingCollection<Frame> Frames { get; } = new(new ConcurrentQueue<Frame>(), 6);

        /// <summary>FFmpeg's last error lines, or null.</summary>
        public string? Error
        {
            get
            {
                lock (_errors)
                    return _errors.Count == 0 ? null : string.Join(" ", _errors.Skip(Math.Max(0, _errors.Count - 2)));
            }
        }

        public static Decoder Start(string ffmpeg, string file, double from, double to, int width, int height, double rate)
        {
            var inv = CultureInfo.InvariantCulture;
            var psi = new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var arg in new[]
                     {
                         "-hide_banner", "-loglevel", "error", "-nostdin",
                         "-ss", from.ToString("0.000", inv), "-i", file,
                         "-t", Math.Max(0.05, to - from).ToString("0.000", inv),
                         "-map", "0:v:0", "-an", "-sn", "-dn",
                         "-vf", $"scale={width}:{height},setsar=1",
                         "-pix_fmt", "bgr24", "-f", "rawvideo", "pipe:1"
                     })
                psi.ArgumentList.Add(arg);

            var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
            Decoder? decoder = null;
            var reader = new Thread(() => decoder!.Read(width * height * 3, from, rate)) { IsBackground = true, Name = "Part player" };
            decoder = new Decoder(process, reader);
            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;
                lock (decoder._errors)
                    decoder._errors.Add(e.Data.Trim());
            };
            process.BeginErrorReadLine();
            reader.Start();
            return decoder;
        }

        private void Read(int frameBytes, double from, double rate)
        {
            try
            {
                var stream = _process.StandardOutput.BaseStream;
                for (int n = 0; !_stopping; n++)
                {
                    var pixels = new byte[frameBytes];
                    int got = 0;
                    while (got < frameBytes)
                    {
                        int read = stream.Read(pixels, got, frameBytes - got);
                        if (read <= 0)
                            return;                 // end of the part (or FFmpeg stopped)
                        got += read;
                    }

                    Frames.Add(new Frame(pixels, from + n / rate));
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // Stopped while reading: nothing to report.
            }
            finally
            {
                try
                {
                    Frames.CompleteAdding();
                }
                catch (ObjectDisposedException)
                {
                    // The player closed meanwhile.
                }
            }
        }

        public void Dispose()
        {
            _stopping = true;
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Already gone.
            }

            // Free the reader if it is waiting for room in the queue.
            while (Frames.TryTake(out _))
            {
            }

            // Dispose the queue only once the reader is done with it.
            if (_reader.Join(2000))
                Frames.Dispose();
            _process.Dispose();
        }
    }
}
