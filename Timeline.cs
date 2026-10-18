using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MockRtsp;

/// <summary>
/// A scrollable timeline for one video: a time ruler, a bar with the skip ranges in red, and
/// the playhead. Hovering shows a preview frame of that moment above the timeline.
///   Click or drag           jump to that moment (SeekRequested, raised on release)
///   Drag a red edge         move where a skip range starts or ends (SkipRangesChanged)
///   Shift + drag            draw a new skip range
///   Right-click             delete a skip range, add one, or clear them all
///   Mouse wheel             scroll; Ctrl + wheel zooms around the mouse
/// While playing, it scrolls by itself to keep the playhead in view, unless the user has
/// just scrolled.
/// </summary>
public sealed class Timeline : Control
{
    private static readonly double[] RulerSteps = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200 };
    private const int EdgeGrab = 6;             // pixels either side of an edge that grab it

    private enum DragMode { None, Seek, Edge, Create }

    private readonly HScrollBar _scroll = new();
    private readonly PreviewPopup _preview = new();
    private readonly Dictionary<int, Bitmap> _bitmaps = new();
    private readonly List<SkipRange> _skips = new();
    private ThumbnailStrip? _strip;
    private double _duration;
    private double _position;
    private double _pixelsPerSecond;            // 0 until the first layout picks a zoom
    private double _scrollSeconds;              // time at the left edge
    private DateTime _userScrolledAt = DateTime.MinValue;
    private bool _updatingScrollBar;

    private DragMode _drag;
    private double _dragSeconds;                // seek target, or the moving end of a new range
    private double _createAnchor;
    private int _edgeRange = -1;
    private bool _edgeIsStart;
    private int _hoverX = -1;

    /// <summary>Raised when the user picks a moment, with the time in seconds.</summary>
    public event EventHandler<double>? SeekRequested;

    /// <summary>Raised after the user adds, moves or deletes skip ranges. Read SkipRanges for the new list.</summary>
    public event EventHandler? SkipRangesChanged;

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

    /// <summary>Shows a video: its length, and the preview frames used on hover. Null when no camera is selected.</summary>
    public void SetSource(ThumbnailStrip? strip, double durationSeconds)
    {
        if (ReferenceEquals(strip, _strip) && Math.Abs(durationSeconds - _duration) < 0.001)
            return;

        ClearBitmaps();
        _strip = strip;
        _duration = Math.Max(0, durationSeconds);
        _pixelsPerSecond = 0;                   // pick a fresh zoom for this video
        _scrollSeconds = 0;
        _drag = DragMode.None;
        _skips.Clear();
        PendingStart = null;
        HidePreview();
        UpdateScrollBar();
        Invalidate();
    }

    /// <summary>The skip ranges shown. Changes from outside are ignored while the user is editing one.</summary>
    public IReadOnlyList<SkipRange> SkipRanges => _skips;

    public void SetSkipRanges(IReadOnlyList<SkipRange> ranges)
    {
        if (_drag is DragMode.Edge or DragMode.Create)
            return;

        if (_skips.SequenceEqual(ranges))
            return;

        _skips.Clear();
        _skips.AddRange(ranges);
        Invalidate();
    }

    /// <summary>Where a skip range started with "Skip start" begins, shown as an orange line. Null for none.</summary>
    public double? PendingStart
    {
        get => _pendingStart;
        set
        {
            _pendingStart = value;
            Invalidate();
        }
    }

    private double? _pendingStart;

    /// <summary>The playhead, in seconds.</summary>
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

    /// <summary>First view of a video: the whole of it, up to 2 minutes; longer ones scroll.</summary>
    private void EnsureZoom()
    {
        if (_pixelsPerSecond > 0 || ViewWidth <= 0 || _duration <= 0)
            return;

        _pixelsPerSecond = Clamp(ViewWidth / Math.Min(_duration, 120), MinPixelsPerSecond, MaxPixelsPerSecond);

        // Open on the playhead, not on the start of the video.
        _scrollSeconds = _position - ViewWidth * 0.1 / _pixelsPerSecond;
        ClampScroll();
    }

    // ===================================================================== geometry

    private int ViewWidth => Math.Max(1, ClientSize.Width);

    private int RulerHeight => Font.Height + 6;

    private Rectangle BarArea =>
        new(0, RulerHeight, ViewWidth, Math.Max(1, ClientSize.Height - RulerHeight - _scroll.Height));

    private int TimeToX(double seconds) => (int)Math.Round((seconds - _scrollSeconds) * _pixelsPerSecond);

    private double XToTime(int x) => Clamp(_scrollSeconds + x / Math.Max(_pixelsPerSecond, 0.0001), 0, _duration);

    private void ClampScroll()
    {
        double visible = ViewWidth / Math.Max(_pixelsPerSecond, 0.0001);
        _scrollSeconds = Clamp(_scrollSeconds, 0, Math.Max(0, _duration - visible));
    }

    private void FollowPlayhead()
    {
        if (_drag != DragMode.None || _pixelsPerSecond <= 0 || DateTime.UtcNow - _userScrolledAt < TimeSpan.FromSeconds(4))
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

    /// <summary>The skip range edge under x, nearest first, or (-1, false).</summary>
    private (int Range, bool IsStart) EdgeAt(int x)
    {
        int best = -1;
        bool bestIsStart = false;
        int bestDistance = EdgeGrab + 1;

        for (int i = 0; i < _skips.Count; i++)
        {
            int ds = Math.Abs(TimeToX(_skips[i].Start) - x);
            int de = Math.Abs(TimeToX(_skips[i].End) - x);

            if (ds < bestDistance) { best = i; bestIsStart = true; bestDistance = ds; }
            if (de < bestDistance) { best = i; bestIsStart = false; bestDistance = de; }
        }

        return (best, bestIsStart);
    }

    private int RangeAt(double seconds) => _skips.FindIndex(r => r.Contains(seconds));

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

            DrawBar(g);
            DrawSkips(g);
            DrawRuler(g);
            DrawStatus(g);

            if (_pendingStart is double pending)
                DrawMarker(g, TimeToX(pending), Color.Orange, "skip from " + FormatPrecise(pending), true);

            if (_hoverX >= 0 && _drag == DragMode.None)
                DrawMarker(g, _hoverX, Color.FromArgb(150, 255, 255, 255), null, false);

            if (_drag == DragMode.Seek)
                DrawMarker(g, TimeToX(_dragSeconds), Color.Gold, FormatTime(_dragSeconds), true);

            DrawMarker(g, TimeToX(_position), Color.FromArgb(235, 64, 52), null, true);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Drawing the timeline");
        }
    }

    private void DrawBar(Graphics g)
    {
        var bar = BarArea;
        int endX = Clamp(TimeToX(_duration), 0, ViewWidth);

        using (var track = new SolidBrush(Color.FromArgb(58, 64, 74)))
            g.FillRectangle(track, 0, bar.Top + 4, endX, bar.Height - 8);

        using (var past = new SolidBrush(Color.FromArgb(20, 20, 22)))
            g.FillRectangle(past, endX, bar.Top, ViewWidth - endX, bar.Height);
    }

    private void DrawSkips(Graphics g)
    {
        var bar = BarArea;
        using var fill = new SolidBrush(Color.FromArgb(170, 190, 38, 38));
        using var hatch = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(60, 255, 255, 255), Color.Transparent);
        using var edge = new SolidBrush(Color.FromArgb(235, 235, 235));
        using var activeEdge = new SolidBrush(Color.Gold);

        var (hoverRange, hoverIsStart) = _hoverX >= 0 && _drag == DragMode.None ? EdgeAt(_hoverX) : (-1, false);

        for (int i = 0; i < _skips.Count; i++)
        {
            int x1 = TimeToX(_skips[i].Start);
            int x2 = TimeToX(_skips[i].End);
            if (x2 < 0 || x1 > ViewWidth)
                continue;

            var rect = new Rectangle(x1, bar.Top + 2, Math.Max(1, x2 - x1), bar.Height - 4);
            g.FillRectangle(fill, rect);
            g.FillRectangle(hatch, rect);

            if (rect.Width > 46)
                TextRenderer.DrawText(g, $"skip {_skips[i].Length:0.0} s", Font, rect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            bool startActive = (_drag == DragMode.Edge && _edgeRange == i && _edgeIsStart) || (hoverRange == i && hoverIsStart);
            bool endActive = (_drag == DragMode.Edge && _edgeRange == i && !_edgeIsStart) || (hoverRange == i && !hoverIsStart);
            DrawEdge(g, x1, rect, startActive ? activeEdge : edge);
            DrawEdge(g, x2, rect, endActive ? activeEdge : edge);
        }

        if (_drag == DragMode.Create)
        {
            int x1 = TimeToX(Math.Min(_createAnchor, _dragSeconds));
            int x2 = TimeToX(Math.Max(_createAnchor, _dragSeconds));
            using var creating = new SolidBrush(Color.FromArgb(150, 255, 140, 0));
            g.FillRectangle(creating, x1, bar.Top + 2, Math.Max(1, x2 - x1), bar.Height - 4);
        }
    }

    /// <summary>A grab handle: a light bar with two grip lines.</summary>
    private static void DrawEdge(Graphics g, int x, Rectangle range, Brush brush)
    {
        g.FillRectangle(brush, x - 2, range.Top, 4, range.Height);
        using var grip = new Pen(Color.FromArgb(90, 0, 0, 0));
        int mid = range.Top + range.Height / 2;
        g.DrawLine(grip, x - 1, mid - 5, x - 1, mid + 5);
        g.DrawLine(grip, x + 1, mid - 5, x + 1, mid + 5);
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
        string? text = _strip is { IsComplete: false, Error: null }
            ? $"Making previews {100 * _strip.Count / Math.Max(1, _strip.ExpectedCount)}%"
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
        var box = new Rectangle(left, 1, size.Width + 4, RulerHeight - 2);
        using (var back = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
            g.FillRectangle(back, box);
        TextRenderer.DrawText(g, label, Font, box, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    // ===================================================================== hover preview

    /// <summary>Shows the frame at a moment in the floating preview, above x.</summary>
    private void ShowPreview(int x, double seconds)
    {
        if (_duration <= 0)
            return;

        int index = _skips.FindIndex(r => r.Contains(seconds));
        string caption = FormatPrecise(seconds) + (index >= 0 ? "  (skipped)" : string.Empty);

        Bitmap? image = BitmapFor(seconds);
        string? note = image is not null ? null
            : _strip is null ? "No preview"
            : _strip.Error is not null ? "No preview: " + _strip.Error
            : $"Making previews {100 * _strip.Count / Math.Max(1, _strip.ExpectedCount)}%";

        var imageSize = _strip is null ? new Size(192, 108) : new Size(_strip.Width, _strip.Height);
        _preview.SetContent(image, imageSize, caption, note, index >= 0);

        // Centred over the mouse, just above the timeline, kept on the screen.
        var screen = Screen.FromControl(this).WorkingArea;
        var origin = PointToScreen(new Point(x, 0));
        int left = Clamp(origin.X - _preview.Width / 2, screen.Left, screen.Right - _preview.Width);
        int top = origin.Y - _preview.Height - 6;
        if (top < screen.Top)
            top = PointToScreen(new Point(0, ClientSize.Height)).Y + 6;

        _preview.Location = new Point(left, top);
        if (!_preview.Visible)
            _preview.Show(FindForm());
    }

    private void HidePreview()
    {
        if (_preview.Visible)
            _preview.Hide();
    }

    /// <summary>The preview frame for a moment, turned into a bitmap the first time it is needed.</summary>
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

    // ===================================================================== mouse

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left || _duration <= 0 || !Enabled || e.Y >= ClientSize.Height - _scroll.Height)
            return;

        var (range, isStart) = EdgeAt(e.X);

        if (range >= 0)
        {
            _drag = DragMode.Edge;
            _edgeRange = range;
            _edgeIsStart = isStart;
        }
        else if ((ModifierKeys & Keys.Shift) != 0)
        {
            _drag = DragMode.Create;
            _createAnchor = _dragSeconds = XToTime(e.X);
        }
        else
        {
            _drag = DragMode.Seek;
            _dragSeconds = XToTime(e.X);
        }

        Capture = true;
        OnMouseMove(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_drag != DragMode.None && (e.X < 0 || e.X > ViewWidth))
        {
            // Dragging past an edge scrolls the timeline.
            _scrollSeconds += (e.X < 0 ? e.X : e.X - ViewWidth) / Math.Max(_pixelsPerSecond, 0.0001) / 4;
            ClampScroll();
            UpdateScrollBar();
        }

        int x = Clamp(e.X, 0, ViewWidth);
        double seconds = XToTime(x);

        switch (_drag)
        {
            case DragMode.Edge:
                seconds = MoveEdge(seconds);
                break;

            case DragMode.Create:
            case DragMode.Seek:
                _dragSeconds = seconds;
                break;

            default:
                bool overBar = e.Y < ClientSize.Height - _scroll.Height;
                _hoverX = overBar ? e.X : -1;
                Cursor = overBar && EdgeAt(e.X).Range >= 0 ? Cursors.SizeWE : Cursors.Default;
                if (!overBar)
                {
                    HidePreview();
                    Invalidate();
                    return;
                }
                break;
        }

        // While editing an edge the preview shows the exact frame at that edge.
        ShowPreview(_drag == DragMode.Edge ? TimeToX(seconds) : x, seconds);
        Invalidate();
    }

    /// <summary>Moves the dragged edge, never past its neighbours and never shorter than the minimum.</summary>
    private double MoveEdge(double seconds)
    {
        var range = _skips[_edgeRange];
        double previousEnd = _edgeRange > 0 ? _skips[_edgeRange - 1].End : 0;
        double nextStart = _edgeRange < _skips.Count - 1 ? _skips[_edgeRange + 1].Start : _duration;

        if (_edgeIsStart)
        {
            seconds = Clamp(seconds, previousEnd, range.End - MockRtsp.SkipRanges.MinLength);
            _skips[_edgeRange] = range with { Start = seconds };
        }
        else
        {
            seconds = Clamp(seconds, range.Start + MockRtsp.SkipRanges.MinLength, nextStart);
            _skips[_edgeRange] = range with { End = seconds };
        }

        return seconds;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Right && _drag == DragMode.None && _duration > 0)
        {
            ShowMenu(e.Location);
            return;
        }

        var drag = _drag;
        _drag = DragMode.None;
        Capture = false;

        switch (drag)
        {
            case DragMode.Seek:
                _position = _dragSeconds;           // show it there straight away
                SeekRequested?.Invoke(this, _dragSeconds);
                break;

            case DragMode.Edge:
                ReplaceSkips(_skips.ToList());
                break;

            case DragMode.Create:
                if (Math.Abs(_dragSeconds - _createAnchor) >= MockRtsp.SkipRanges.MinLength)
                    ReplaceSkips(_skips.Append(new SkipRange(Math.Min(_createAnchor, _dragSeconds), Math.Max(_createAnchor, _dragSeconds))).ToList());
                break;
        }

        Invalidate();
    }

    private void ShowMenu(Point location)
    {
        HidePreview();
        double seconds = XToTime(location.X);
        int index = RangeAt(seconds);
        var menu = new ContextMenuStrip();

        if (index >= 0)
        {
            var range = _skips[index];
            menu.Items.Add($"Delete skip {FormatPrecise(range.Start)} - {FormatPrecise(range.End)}", null,
                (_, _) => ReplaceSkips(_skips.Where((_, i) => i != index).ToList()));
        }
        else
        {
            menu.Items.Add($"Add a 5 s skip at {FormatPrecise(seconds)}", null,
                (_, _) => ReplaceSkips(_skips.Append(new SkipRange(seconds, Math.Min(_duration, seconds + 5))).ToList()));
        }

        var clear = menu.Items.Add("Clear all skips", null, (_, _) => ReplaceSkips(new List<SkipRange>()));
        clear.Enabled = _skips.Count > 0;

        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
        menu.Show(this, location);
    }

    private void ReplaceSkips(List<SkipRange> ranges)
    {
        var normalized = MockRtsp.SkipRanges.Normalize(ranges, _duration);
        _skips.Clear();
        _skips.AddRange(normalized);
        Invalidate();
        SkipRangesChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverX = -1;
        if (_drag == DragMode.None)
            HidePreview();
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
        {
            ClearBitmaps();
            _preview.Dispose();
        }

        base.Dispose(disposing);
    }

    // ===================================================================== helpers

    private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    /// <summary>With tenths of a second, for skip edges, e.g. "01:23.4".</summary>
    private static string FormatPrecise(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, Math.Round(seconds, 1)));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss\.f") : time.ToString(@"mm\:ss\.f");
    }

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");
    }

    /// <summary>
    /// The floating preview: a borderless window that never takes focus, showing one frame
    /// and its time.
    /// </summary>
    private sealed class PreviewPopup : Form
    {
        private Bitmap? _image;
        private string _caption = string.Empty;
        private string? _note;
        private bool _skipped;
        private Size _imageSize = new(192, 108);

        public PreviewPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x80;
                const int WS_EX_NOACTIVATE = 0x08000000;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void SetContent(Bitmap? image, Size imageSize, string caption, string? note, bool skipped)
        {
            _image = image;
            _imageSize = imageSize;
            _caption = caption;
            _note = note;
            _skipped = skipped;

            int captionHeight = Font.Height + 6;
            var size = new Size(_imageSize.Width + 4, _imageSize.Height + captionHeight + 4);
            if (Size != size)
                Size = size;

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var imageRect = new Rectangle(2, 2, _imageSize.Width, _imageSize.Height);

            if (_image is not null)
            {
                g.DrawImage(_image, imageRect);
            }
            else
            {
                using var empty = new SolidBrush(Color.FromArgb(40, 40, 44));
                g.FillRectangle(empty, imageRect);
                TextRenderer.DrawText(g, _note ?? string.Empty, Font, imageRect, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }

            if (_skipped)
            {
                using var tint = new SolidBrush(Color.FromArgb(90, 200, 30, 30));
                g.FillRectangle(tint, imageRect);
            }

            var captionRect = new Rectangle(2, imageRect.Bottom, _imageSize.Width, ClientSize.Height - imageRect.Bottom - 2);
            TextRenderer.DrawText(g, _caption, Font, captionRect, _skipped ? Color.LightCoral : Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using var border = new Pen(Color.FromArgb(110, 110, 120));
            g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }
}
