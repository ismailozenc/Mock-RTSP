using Birel_Plaka;
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
/// It also shows where plates are read (<see cref="Area"/>) and lets it be drawn, as on a camera in
/// Birel Plaka: R edits the detection area, P the road perspective (see <see cref="SetEditing"/>).
/// </summary>
/// <summary>What the player's mouse edits: nothing (play, zoom), the detection area or the road perspective.</summary>
public enum AreaEdit { None, Detection, Perspective }

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

    // Plate reading area (fractions of the picture) and its editing.
    private ReadArea _area = ReadArea.WholePicture;
    private AreaEdit _editing = AreaEdit.None;
    private ReadArea? _areaAtEditStart;
    private Grip _grip = Grip.None;             // what the left button holds in detection editing
    private RectangleF _rectAtGrip;
    private PointF _gripFrom;
    private int _corner = -1;                   // perspective: 0-3 a corner, 4 the whole shape
    private PointF[]? _cornersAtGrip;
    private const int HandleSize = 8;
    private const int CornerGrab = 12;
    private const float MinAreaSize = 0.02f;

    private enum Grip { None, Body, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>Raised when the user changes the area (after each drag, a reset or an undo).</summary>
    public event EventHandler? AreaChanged;

    /// <summary>Raised when editing starts or stops.</summary>
    public event EventHandler? EditingChanged;

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
                DrawArea(g);

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
        if (_editing != AreaEdit.None && _picture is not null && VideoArea.Contains(e.Location))
        {
            AreaMouseDown(e);
            return;
        }

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

        if (_editing != AreaEdit.None && !_seekDragging)
        {
            AreaMouseMove(e);
            return;
        }

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

        if (_grip != Grip.None || _corner >= 0)
        {
            _grip = Grip.None;
            _corner = -1;
            _cornersAtGrip = null;
            AreaChanged?.Invoke(this, EventArgs.Empty);
            Invalidate(VideoArea);
            return;
        }

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
        keyData is Keys.Left or Keys.Right or Keys.Space
        || (keyData == Keys.Escape && _editing != AreaEdit.None)     // undo, not the dialog's Cancel
        || base.IsInputKey(keyData);

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
            case Keys.R:
                SetEditing(_editing == AreaEdit.Detection ? AreaEdit.None : AreaEdit.Detection);
                e.Handled = true;
                break;
            case Keys.P:
                SetEditing(_editing == AreaEdit.Perspective ? AreaEdit.None : AreaEdit.Perspective);
                e.Handled = true;
                break;
            case Keys.Escape when _editing != AreaEdit.None && _areaAtEditStart is not null:
                _grip = Grip.None;
                _corner = -1;
                _area = _areaAtEditStart;               // undo everything since editing started
                AreaChanged?.Invoke(this, EventArgs.Empty);
                Invalidate(VideoArea);
                e.Handled = true;
                break;
        }
    }

    // ===================================================================== plate reading area

    /// <summary>Where plates are read, shown on the picture (fractions of the picture).</summary>
    public ReadArea Area
    {
        get => _area;
        set
        {
            _area = value;
            Invalidate(VideoArea);
        }
    }

    public AreaEdit Editing => _editing;

    /// <summary>
    /// Starts or stops editing the detection area or the road perspective. Starting the perspective
    /// with none drawn places Birel Plaka's default shape.
    /// </summary>
    public void SetEditing(AreaEdit editing)
    {
        if (editing == _editing)
            return;

        _grip = Grip.None;
        _corner = -1;
        _editing = editing;
        _areaAtEditStart = editing == AreaEdit.None ? null : _area;

        if (editing == AreaEdit.Perspective && _area.Perspective is null)
        {
            _area = _area.WithPerspective(RoadPerspective.DefaultCorners(new RectangleF(0, 0, 1, 1)));
            AreaChanged?.Invoke(this, EventArgs.Empty);
        }

        if (editing != AreaEdit.None)
            Focus();
        Cursor = Cursors.Default;
        Invalidate(VideoArea);
        EditingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A picture point (fractions) on the screen, at the current zoom.</summary>
    private PointF ToScreen(PointF p)
    {
        var fit = FitRect();
        var src = SourceRect();
        return new PointF(
            fit.X + (p.X * _width - src.X) / src.Width * fit.Width,
            fit.Y + (p.Y * _height - src.Y) / src.Height * fit.Height);
    }

    /// <summary>The picture point (fractions, clamped to the picture) under a screen point.</summary>
    private PointF ToPicture(Point p)
    {
        var fit = FitRect();
        var src = SourceRect();
        return new PointF(
            Clamp((src.X + (p.X - fit.X) / fit.Width * src.Width) / _width, 0, 1),
            Clamp((src.Y + (p.Y - fit.Y) / fit.Height * src.Height) / _height, 0, 1));
    }

    private RectangleF ScreenRect(RectangleF r)
    {
        var a = ToScreen(r.Location);
        var b = ToScreen(new PointF(r.Right, r.Bottom));
        return RectangleF.FromLTRB(a.X, a.Y, b.X, b.Y);
    }

    private RectangleF DetectionOrWhole => _area.Detection ?? new RectangleF(0, 0, 1, 1);

    private void DrawArea(Graphics g)
    {
        var fit = FitRect();
        var clip = g.Clip;
        g.SetClip(fit);
        var smoothing = g.SmoothingMode;
        try
        {
            DrawPerspective(g);

            if (_editing == AreaEdit.Detection || _area.Detection is not null)
            {
                var rect = ScreenRect(DetectionOrWhole);
                if (_editing == AreaEdit.Detection)
                {
                    // Darker outside the area: what the detector never sees.
                    using var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
                    g.FillRectangle(dim, fit.Left, fit.Top, fit.Width, rect.Top - fit.Top);
                    g.FillRectangle(dim, fit.Left, rect.Bottom, fit.Width, fit.Bottom - rect.Bottom);
                    g.FillRectangle(dim, fit.Left, rect.Top, rect.Left - fit.Left, rect.Height);
                    g.FillRectangle(dim, rect.Right, rect.Top, fit.Right - rect.Right, rect.Height);
                }

                using var pen = new Pen(Color.Lime, 2);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

                if (_editing == AreaEdit.Detection)
                {
                    foreach (var handle in Handles(rect))
                        g.FillRectangle(Brushes.Lime, handle);
                }
            }
        }
        finally
        {
            g.SmoothingMode = smoothing;
            g.Clip = clip;
        }

        if (_editing == AreaEdit.Detection)
            DrawHint(g, _area.Detection is null
                ? "Detection area: drag over the picture to draw it (now the whole picture is read). Esc: undo. R: done."
                : "Detection area: drag its edges or corners, drag inside to move it, drag outside it to draw a new one. "
                  + "Right-click: whole picture. Esc: undo. R: done.");
        else if (_editing == AreaEdit.Perspective)
            DrawHint(g, "Road perspective: drag the 4 corners onto the lane edges (a rectangle lying on the road). "
                        + "Drag inside to move it. Right-click: reset. Esc: undo. P: done.");
    }

    private void DrawPerspective(Graphics g)
    {
        if (_area.Perspective is not { } corners)
            return;

        var shown = corners.Select(ToScreen).ToArray();
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_editing != AreaEdit.Perspective)
        {
            using var faint = new Pen(Color.FromArgb(170, Color.Gold), 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawPolygon(faint, shown);
            return;
        }

        // A grid "painted on the road": if it looks like evenly spaced marks lying flat on the road, it is right.
        if (RoadPerspective.Create(shown, minArea: 100) is { } road)
        {
            using var grid = new Pen(Color.FromArgb(150, Color.Gold), 1f);
            for (int i = 1; i < 4; i++)
                g.DrawLine(grid, road.ToPicture(i / 4.0, 0), road.ToPicture(i / 4.0, 1));
            for (int i = 1; i < 6; i++)
                g.DrawLine(grid, road.ToPicture(0, i / 6.0), road.ToPicture(1, i / 6.0));
        }

        using (var pen = new Pen(Color.Gold, 2f))
            g.DrawPolygon(pen, shown);
        foreach (var c in shown)
            g.FillEllipse(Brushes.Gold, c.X - 5, c.Y - 5, 10, 10);

        DrawLabel(g, "far", Middle(shown[RoadPerspective.FarLeft], shown[RoadPerspective.FarRight]), above: true);
        DrawLabel(g, "near", Middle(shown[RoadPerspective.NearRight], shown[RoadPerspective.NearLeft]), above: false);
    }

    private static PointF Middle(PointF a, PointF b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private void DrawLabel(Graphics g, string text, PointF at, bool above)
    {
        var size = TextRenderer.MeasureText(text, Font);
        float y = above ? at.Y - size.Height - 6 : at.Y + 6;
        var box = new Rectangle((int)(at.X - size.Width / 2f) - 3, (int)y, size.Width + 6, size.Height);
        using var back = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
        g.FillRectangle(back, box);
        TextRenderer.DrawText(g, text, Font, box, Color.Gold, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    /// <summary>How to edit, at the bottom of the picture (the zoom note is at the top).</summary>
    private void DrawHint(Graphics g, string text)
    {
        var area = VideoArea;
        int width = Math.Max(80, area.Width - 12);
        var size = TextRenderer.MeasureText(g, text, Font, new Size(width, 0), TextFormatFlags.WordBreak);
        var box = new Rectangle(area.Left + 6, area.Bottom - size.Height - 8, width, size.Height + 4);
        using var back = new SolidBrush(Color.FromArgb(185, 0, 0, 0));
        g.FillRectangle(back, box);
        TextRenderer.DrawText(g, text, Font, box, Color.White, TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter);
    }

    private static RectangleF[] Handles(RectangleF r)
    {
        float s = HandleSize, h = s / 2;
        return new[]
        {
            new RectangleF(r.Left - h, r.Top - h, s, s), new RectangleF(r.Right - h, r.Top - h, s, s),
            new RectangleF(r.Left - h, r.Bottom - h, s, s), new RectangleF(r.Right - h, r.Bottom - h, s, s),
            new RectangleF(r.Left - h, r.Top + r.Height / 2 - h, s, s), new RectangleF(r.Right - h, r.Top + r.Height / 2 - h, s, s),
            new RectangleF(r.Left + r.Width / 2 - h, r.Top - h, s, s), new RectangleF(r.Left + r.Width / 2 - h, r.Bottom - h, s, s)
        };
    }

    /// <summary>The corner handles, then any point near an edge, then inside.</summary>
    private Grip GripAt(Point p)
    {
        var rect = ScreenRect(DetectionOrWhole);
        var h = Handles(rect).Select(r => RectangleF.Inflate(r, 3, 3)).ToArray();
        if (h[0].Contains(p)) return Grip.TopLeft;
        if (h[1].Contains(p)) return Grip.TopRight;
        if (h[2].Contains(p)) return Grip.BottomLeft;
        if (h[3].Contains(p)) return Grip.BottomRight;

        const float Near = 6;
        bool alongX = p.X >= rect.Left - Near && p.X <= rect.Right + Near;
        bool alongY = p.Y >= rect.Top - Near && p.Y <= rect.Bottom + Near;
        if (alongY && Math.Abs(p.X - rect.Left) <= Near) return Grip.Left;
        if (alongY && Math.Abs(p.X - rect.Right) <= Near) return Grip.Right;
        if (alongX && Math.Abs(p.Y - rect.Top) <= Near) return Grip.Top;
        if (alongX && Math.Abs(p.Y - rect.Bottom) <= Near) return Grip.Bottom;
        return rect.Contains(p) ? Grip.Body : Grip.None;
    }

    private int CornerAt(Point p)
    {
        if (_area.Perspective is not { } corners)
            return -1;

        var shown = corners.Select(ToScreen).ToArray();
        for (int i = 0; i < 4; i++)
        {
            float dx = p.X - shown[i].X, dy = p.Y - shown[i].Y;
            if (dx * dx + dy * dy <= CornerGrab * CornerGrab)
                return i;
        }

        using var path = new GraphicsPath();
        path.AddPolygon(shown);
        return path.IsVisible(p) ? 4 : -1;
    }

    private void AreaMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _area = _editing == AreaEdit.Detection
                ? _area.WithDetection(null)
                : _area.WithPerspective(RoadPerspective.DefaultCorners(new RectangleF(0, 0, 1, 1)));
            AreaChanged?.Invoke(this, EventArgs.Empty);
            Invalidate(VideoArea);
            return;
        }

        if (e.Button != MouseButtons.Left)
            return;

        _gripFrom = ToPicture(e.Location);
        if (_editing == AreaEdit.Detection)
        {
            _rectAtGrip = DetectionOrWhole;
            _grip = _area.Detection is null ? Grip.None : GripAt(e.Location);
            if (_grip == Grip.None)
            {
                // A new area from here (with none drawn yet, the first drag draws it).
                _rectAtGrip = new RectangleF(_gripFrom, SizeF.Empty);
                _grip = Grip.BottomRight;
            }
        }
        else
        {
            _corner = CornerAt(e.Location);
            _cornersAtGrip = _area.Perspective?.ToArray();
        }
    }

    private void AreaMouseMove(MouseEventArgs e)
    {
        if (_editing == AreaEdit.Detection)
        {
            if (_grip == Grip.None)
            {
                Cursor = GripAt(e.Location) switch
                {
                    Grip.Body => Cursors.SizeAll,
                    Grip.Left or Grip.Right => Cursors.SizeWE,
                    Grip.Top or Grip.Bottom => Cursors.SizeNS,
                    Grip.TopLeft or Grip.BottomRight => Cursors.SizeNWSE,
                    Grip.TopRight or Grip.BottomLeft => Cursors.SizeNESW,
                    _ => Cursors.Cross
                };
                return;
            }

            var at = ToPicture(e.Location);
            var r = _rectAtGrip;
            float left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;
            if (_grip == Grip.Body)
            {
                float dx = Clamp(at.X - _gripFrom.X, -r.Left, 1 - r.Right);
                float dy = Clamp(at.Y - _gripFrom.Y, -r.Top, 1 - r.Bottom);
                left += dx; right += dx; top += dy; bottom += dy;
            }
            else
            {
                if (_grip is Grip.Left or Grip.TopLeft or Grip.BottomLeft) left = at.X;
                if (_grip is Grip.Right or Grip.TopRight or Grip.BottomRight) right = at.X;
                if (_grip is Grip.Top or Grip.TopLeft or Grip.TopRight) top = at.Y;
                if (_grip is Grip.Bottom or Grip.BottomLeft or Grip.BottomRight) bottom = at.Y;
            }

            var moved = RectangleF.FromLTRB(Math.Min(left, right), Math.Min(top, bottom), Math.Max(left, right), Math.Max(top, bottom));
            // At least a small area, kept inside the picture.
            if (moved.Width < MinAreaSize)
                moved.Width = MinAreaSize;
            if (moved.Height < MinAreaSize)
                moved.Height = MinAreaSize;
            moved.X = Math.Min(moved.X, 1 - moved.Width);
            moved.Y = Math.Min(moved.Y, 1 - moved.Height);
            _area = _area.WithDetection(moved);
            Invalidate(VideoArea);
            return;
        }

        if (_corner < 0 || _cornersAtGrip is null)
        {
            Cursor = CornerAt(e.Location) switch
            {
                >= 0 and < 4 => Cursors.Hand,
                4 => Cursors.SizeAll,
                _ => Cursors.Default
            };
            return;
        }

        var point = ToPicture(e.Location);
        var corners = _cornersAtGrip.ToArray();
        if (_corner == 4)
        {
            // The whole shape, kept inside the picture.
            float dx = Clamp(point.X - _gripFrom.X, -corners.Min(p => p.X), 1 - corners.Max(p => p.X));
            float dy = Clamp(point.Y - _gripFrom.Y, -corners.Min(p => p.Y), 1 - corners.Max(p => p.Y));
            for (int i = 0; i < 4; i++)
                corners[i] = new PointF(corners[i].X + dx, corners[i].Y + dy);
        }
        else
        {
            corners[_corner] = point;
        }

        // Only a proper (convex, not crossing) shape, at least about 10 x 10 picture pixels.
        if (RoadPerspective.IsValidShape(corners.Select(p => new PointF(p.X * _width, p.Y * _height)).ToArray(), minArea: 100))
        {
            _area = _area.WithPerspective(corners);
            Invalidate(VideoArea);
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
