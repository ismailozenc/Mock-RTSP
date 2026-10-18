using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MockRtsp;

/// <summary>
/// A scrollable video timeline: a time ruler, a strip of thumbnails and a playhead.
///   Click or drag     jump to that moment (SeekRequested is raised on release)
///   Mouse wheel       scroll left and right
///   Ctrl + wheel      zoom in and out around the mouse
/// While playing, it scrolls by itself to keep the playhead in view, unless the user has
/// just scrolled.
/// </summary>
public sealed class Timeline : Control
{
    private static readonly double[] RulerSteps = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200 };

    private readonly HScrollBar _scroll = new();
    private readonly Dictionary<int, Bitmap> _bitmaps = new();
    private ThumbnailStrip? _strip;
    private double _duration;
    private double _position;
    private double _pixelsPerSecond;            // 0 until the first layout picks a zoom
    private double _scrollSeconds;              // time at the left edge
    private bool _dragging;
    private double _dragSeconds;
    private int _hoverX = -1;
    private DateTime _userScrolledAt = DateTime.MinValue;
    private bool _updatingScrollBar;

    /// <summary>Raised when the user picks a moment, with the time in seconds.</summary>
    public event EventHandler<double>? SeekRequested;

    public Timeline()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);

        BackColor = Color.FromArgb(32, 32, 36);
        ForeColor = Color.Gainsboro;

        _scroll.Dock = DockStyle.Bottom;
        _scroll.Scroll += OnScrollBarScroll;
        Controls.Add(_scroll);
    }

    // ===================================================================== what to show

    /// <summary>Shows a video's thumbnails. Pass null when no camera is selected.</summary>
    public void SetSource(ThumbnailStrip? strip, double durationSeconds)
    {
        if (ReferenceEquals(strip, _strip) && Math.Abs(durationSeconds - _duration) < 0.001)
            return;

        ClearBitmaps();
        _strip = strip;
        _duration = Math.Max(0, durationSeconds);
        _pixelsPerSecond = 0;                   // pick a fresh zoom for this video
        _scrollSeconds = 0;
        _dragging = false;
        UpdateScrollBar();
        Invalidate();
    }

    /// <summary>The playhead, in seconds. Ignored while the user drags.</summary>
    public double Position
    {
        get => _position;
        set
        {
            if (Math.Abs(value - _position) < 0.001)
                return;

            _position = Math.Max(0, value);
            FollowPlayhead();
            Invalidate();
        }
    }

    // ===================================================================== zoom

    public void ZoomIn() => ZoomAround(TimeToX(_position), 1.5);

    public void ZoomOut() => ZoomAround(TimeToX(_position), 1 / 1.5);

    /// <summary>Shows the whole video.</summary>
    public void ZoomToFit()
    {
        _pixelsPerSecond = MinPixelsPerSecond;
        _scrollSeconds = 0;
        UpdateScrollBar();
        Invalidate();
    }

    private double MinPixelsPerSecond => _duration > 0 ? Math.Max(0.01, ViewWidth / _duration) : 1;

    private static double MaxPixelsPerSecond => 400;

    private void ZoomAround(int x, double factor)
    {
        EnsureZoom();
        double anchor = XToTime(x);
        _pixelsPerSecond = Clamp(_pixelsPerSecond * factor, MinPixelsPerSecond, Math.Max(MinPixelsPerSecond, MaxPixelsPerSecond));
        _scrollSeconds = anchor - x / _pixelsPerSecond;
        ClampScroll();
        UpdateScrollBar();
        Invalidate();
    }

    /// <summary>First view of a video: thumbnails at their own width, about one per second.</summary>
    private void EnsureZoom()
    {
        if (_pixelsPerSecond > 0 || ViewWidth <= 0)
            return;

        double natural = _strip is null ? 60 : TileWidth / _strip.IntervalSeconds;
        _pixelsPerSecond = Clamp(natural, MinPixelsPerSecond, MaxPixelsPerSecond);

        // Open on the playhead, not on the start of the video.
        _scrollSeconds = _position - ViewWidth * 0.1 / _pixelsPerSecond;
        ClampScroll();
    }

    // ===================================================================== geometry

    private int ViewWidth => Math.Max(1, ClientSize.Width);

    private int RulerHeight => Font.Height + 6;

    private Rectangle ThumbArea =>
        new(0, RulerHeight, ViewWidth, Math.Max(1, ClientSize.Height - RulerHeight - _scroll.Height));

    /// <summary>Width of one thumbnail drawn at the strip's height, keeping its shape.</summary>
    private int TileWidth
    {
        get
        {
            if (_strip is null || _strip.Height == 0)
                return 96;

            return Math.Max(8, (int)Math.Round(ThumbArea.Height * (double)_strip.Width / _strip.Height));
        }
    }

    private int TimeToX(double seconds) => (int)Math.Round((seconds - _scrollSeconds) * _pixelsPerSecond);

    private double XToTime(int x) => Clamp(_scrollSeconds + x / Math.Max(_pixelsPerSecond, 0.0001), 0, _duration);

    private void ClampScroll()
    {
        double visible = ViewWidth / Math.Max(_pixelsPerSecond, 0.0001);
        _scrollSeconds = Clamp(_scrollSeconds, 0, Math.Max(0, _duration - visible));
    }

    private void FollowPlayhead()
    {
        if (_dragging || _pixelsPerSecond <= 0 || DateTime.UtcNow - _userScrolledAt < TimeSpan.FromSeconds(4))
            return;

        int x = TimeToX(_position);
        if (x < 0 || x > ViewWidth - 24)
        {
            _scrollSeconds = _position - ViewWidth * 0.1 / _pixelsPerSecond;
            ClampScroll();
            UpdateScrollBar();
        }
    }

    private void UpdateScrollBar()
    {
        _updatingScrollBar = true;
        try
        {
            EnsureZoom();
            int total = (int)Math.Ceiling(_duration * Math.Max(_pixelsPerSecond, 0));
            _scroll.Enabled = total > ViewWidth;
            _scroll.Minimum = 0;
            _scroll.Maximum = Math.Max(total, 1);
            _scroll.LargeChange = Math.Max(1, ViewWidth);
            _scroll.SmallChange = Math.Max(1, ViewWidth / 10);
            _scroll.Value = Clamp((int)(_scrollSeconds * _pixelsPerSecond), 0, Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1));
        }
        finally
        {
            _updatingScrollBar = false;
        }
    }

    // ===================================================================== painting

    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            EnsureZoom();
            var g = e.Graphics;
            g.Clear(BackColor);

            if (_duration <= 0)
            {
                TextRenderer.DrawText(g, "Select a camera to see its timeline.", Font, ClientRectangle, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            DrawThumbnails(g);
            DrawRuler(g);
            DrawStatus(g);

            if (_hoverX >= 0 && !_dragging)
                DrawMarker(g, _hoverX, Color.FromArgb(150, 255, 255, 255), FormatTime(XToTime(_hoverX)), false);

            if (_dragging)
                DrawMarker(g, TimeToX(_dragSeconds), Color.Gold, FormatTime(_dragSeconds), true);

            DrawMarker(g, TimeToX(_position), Color.FromArgb(235, 64, 52), null, true);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Drawing the timeline");
        }
    }

    private void DrawThumbnails(Graphics g)
    {
        var area = ThumbArea;
        int tile = TileWidth;
        int endX = TimeToX(_duration);

        using (var past = new SolidBrush(Color.FromArgb(20, 20, 22)))
            g.FillRectangle(past, Math.Max(endX, 0), area.Top, Math.Max(0, ViewWidth - endX), area.Height);

        // Tiles sit at fixed positions on the whole timeline, so they do not shimmer while scrolling.
        double startPx = _scrollSeconds * _pixelsPerSecond;
        long firstTile = (long)Math.Floor(startPx / tile);

        using var placeholder = new SolidBrush(Color.FromArgb(48, 48, 54));
        using var gap = new Pen(Color.FromArgb(32, 32, 36));

        for (long n = firstTile; ; n++)
        {
            int x = (int)Math.Round(n * tile - startPx);
            if (x >= ViewWidth || x >= endX)
                break;

            double seconds = n * tile / _pixelsPerSecond;
            int width = Math.Min(tile, endX - x);
            var bitmap = BitmapFor(seconds);

            if (bitmap is null)
            {
                g.FillRectangle(placeholder, x, area.Top, width, area.Height);
            }
            else
            {
                var source = new Rectangle(0, 0, (int)Math.Round(bitmap.Width * (double)width / tile), bitmap.Height);
                g.DrawImage(bitmap, new Rectangle(x, area.Top, width, area.Height), source, GraphicsUnit.Pixel);
            }

            g.DrawLine(gap, x, area.Top, x, area.Bottom);
        }
    }

    private void DrawRuler(Graphics g)
    {
        int height = RulerHeight;

        using (var back = new SolidBrush(Color.FromArgb(24, 24, 27)))
            g.FillRectangle(back, 0, 0, ViewWidth, height);

        // Labelled ticks at least ~80 px apart, with 5 small ticks between them.
        double step = RulerSteps[RulerSteps.Length - 1];
        foreach (double candidate in RulerSteps)
        {
            if (candidate * _pixelsPerSecond >= 80)
            {
                step = candidate;
                break;
            }
        }
        double minor = step / 5;
        double first = Math.Floor(_scrollSeconds / minor) * minor;

        using var tickPen = new Pen(Color.FromArgb(120, 120, 128));
        for (double t = first; t <= _duration + 0.0001; t += minor)
        {
            int x = TimeToX(t);
            if (x > ViewWidth)
                break;

            bool major = Math.Abs(t / step - Math.Round(t / step)) < 0.0001;
            g.DrawLine(tickPen, x, height - (major ? 8 : 4), x, height);

            if (major)
                TextRenderer.DrawText(g, FormatTime(t), Font, new Point(x + 3, 2), ForeColor);
        }
    }

    private void DrawStatus(Graphics g)
    {
        if (_strip is null)
            return;

        string? text = _strip.Error is not null
            ? "No thumbnails: " + _strip.Error
            : !_strip.IsComplete
                ? $"Making thumbnails {100 * _strip.Count / Math.Max(1, _strip.ExpectedCount)}%"
                : null;

        if (text is null)
            return;

        var size = TextRenderer.MeasureText(text, Font);
        var box = new Rectangle(ViewWidth - size.Width - 10, 1, size.Width + 8, RulerHeight - 2);
        using (var back = new SolidBrush(Color.FromArgb(200, 24, 24, 27)))
            g.FillRectangle(back, box);
        TextRenderer.DrawText(g, text, Font, box, Color.Khaki, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    /// <summary>A vertical line through the timeline, with an optional time label.</summary>
    private void DrawMarker(Graphics g, int x, Color color, string? label, bool strong)
    {
        if (x < -1 || x > ViewWidth + 1)
            return;

        int bottom = ClientSize.Height - _scroll.Height;
        using (var pen = new Pen(color, strong ? 2 : 1))
            g.DrawLine(pen, x, 0, x, bottom);

        if (strong)
        {
            using var brush = new SolidBrush(color);
            g.FillPolygon(brush, new[] { new Point(x - 6, 0), new Point(x + 6, 0), new Point(x, 7) });
        }

        if (label is null)
            return;

        var size = TextRenderer.MeasureText(label, Font);
        int left = Clamp(x + 4, 0, Math.Max(0, ViewWidth - size.Width - 6));
        var box = new Rectangle(left, ThumbArea.Top + 2, size.Width + 4, size.Height);
        using (var back = new SolidBrush(Color.FromArgb(210, 0, 0, 0)))
            g.FillRectangle(back, box);
        TextRenderer.DrawText(g, label, Font, box, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    /// <summary>The thumbnail for a moment, turned into a bitmap the first time it is drawn.</summary>
    private Bitmap? BitmapFor(double seconds)
    {
        if (_strip is null)
            return null;

        int index = (int)Math.Floor(seconds / _strip.IntervalSeconds + 0.0001);
        if (_bitmaps.TryGetValue(index, out var cached))
            return cached;

        var frame = _strip.Get(index);
        if (frame is null)
            return null;

        var bitmap = ToBitmap(frame, _strip.Width, _strip.Height);
        _bitmaps[index] = bitmap;
        return bitmap;
    }

    private static Bitmap ToBitmap(byte[] bgr, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            // Bitmap rows are padded to 4 bytes; FFmpeg's are not, so copy row by row.
            int row = width * 3;
            for (int y = 0; y < height; y++)
                Marshal.Copy(bgr, y * row, data.Scan0 + y * data.Stride, row);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private void ClearBitmaps()
    {
        foreach (var bitmap in _bitmaps.Values)
            bitmap.Dispose();
        _bitmaps.Clear();
    }

    private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");
    }

    // ===================================================================== mouse

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left || _duration <= 0 || !Enabled)
            return;

        _dragging = true;
        _dragSeconds = XToTime(e.X);
        Capture = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
        {
            // Dragging past an edge scrolls the timeline.
            if (e.X < 0 || e.X > ViewWidth)
            {
                _scrollSeconds += (e.X < 0 ? e.X : e.X - ViewWidth) / Math.Max(_pixelsPerSecond, 0.0001) / 4;
                ClampScroll();
                UpdateScrollBar();
            }

            _dragSeconds = XToTime(Clamp(e.X, 0, ViewWidth));
        }
        else
        {
            _hoverX = e.Y < ClientSize.Height - _scroll.Height ? e.X : -1;
        }

        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_dragging)
            return;

        _dragging = false;
        Capture = false;
        _position = _dragSeconds;                   // show it there straight away
        Invalidate();
        SeekRequested?.Invoke(this, _dragSeconds);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverX = -1;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_duration <= 0)
            return;

        if ((ModifierKeys & Keys.Control) != 0)
        {
            ZoomAround(e.X, Math.Pow(1.25, e.Delta / 120.0));
        }
        else
        {
            _scrollSeconds -= e.Delta / 120.0 * ViewWidth * 0.15 / Math.Max(_pixelsPerSecond, 0.0001);
            ClampScroll();
            UpdateScrollBar();
            Invalidate();
        }

        _userScrolledAt = DateTime.UtcNow;
    }

    private void OnScrollBarScroll(object? sender, ScrollEventArgs e)
    {
        if (_updatingScrollBar || _pixelsPerSecond <= 0)
            return;

        _scrollSeconds = e.NewValue / _pixelsPerSecond;
        ClampScroll();
        _userScrolledAt = DateTime.UtcNow;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_pixelsPerSecond > 0)
            _pixelsPerSecond = Math.Max(_pixelsPerSecond, MinPixelsPerSecond);
        ClampScroll();
        UpdateScrollBar();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ClearBitmaps();

        base.Dispose(disposing);
    }
}
