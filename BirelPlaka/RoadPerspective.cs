namespace Birel_Plaka;

/// <summary>
/// The road surface a camera sees, given as four corners drawn on the picture. The four corners are
/// assumed to be a rectangle lying flat on the road, so they define a perspective mapping
/// (homography) between the picture and a top-down "road map":
///
///     road (u, v), u = 0 left edge .. 1 right edge, v = 0 far edge .. 1 near edge
///         Corner order: 0 = far-left (0,0), 1 = far-right (1,0), 2 = near-right (1,1), 3 = near-left (0,1).
///
/// Points may be in any coordinate system (normalized 0..1 video coordinates, pixels, screen); the
/// mapping works in whatever system the corners are given in.
/// </summary>
public sealed class RoadPerspective
{
    public const int FarLeft = 0, FarRight = 1, NearRight = 2, NearLeft = 3;

    private readonly double _a, _b, _c, _d, _e, _f, _g, _h;   // road -> picture
    private readonly double[] _inverse;                       // picture -> road (3x3, row-major)

    public IReadOnlyList<PointF> Corners { get; }

    private RoadPerspective(PointF[] corners)
    {
        Corners = Array.AsReadOnly((PointF[])corners.Clone());

        // Unit square -> quadrilateral (Heckbert, "Fundamentals of Texture Mapping", 1989).
        double x0 = corners[0].X, y0 = corners[0].Y;
        double x1 = corners[1].X, y1 = corners[1].Y;
        double x2 = corners[2].X, y2 = corners[2].Y;
        double x3 = corners[3].X, y3 = corners[3].Y;

        double dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
        double dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;

        double g = 0, h = 0;
        if (Math.Abs(dx3) > 1e-12 || Math.Abs(dy3) > 1e-12)
        {
            double den = dx1 * dy2 - dx2 * dy1;
            g = (dx3 * dy2 - dx2 * dy3) / den;
            h = (dx1 * dy3 - dx3 * dy1) / den;
        }

        _a = x1 - x0 + g * x1;
        _b = x3 - x0 + h * x3;
        _c = x0;
        _d = y1 - y0 + g * y1;
        _e = y3 - y0 + h * y3;
        _f = y0;
        _g = g;
        _h = h;

        // Inverse of [[a b c][d e f][g h 1]] via the adjugate (the scale factor cancels out).
        _inverse = new[]
        {
            _e - _f * _h,      _c * _h - _b,      _b * _f - _c * _e,
            _f * _g - _d,      _a - _c * _g,      _c * _d - _a * _f,
            _d * _h - _e * _g, _b * _g - _a * _h, _a * _e - _b * _d
        };
    }

    /// <summary>
    /// The perspective for four corners (far-left, far-right, near-right, near-left), or null when they
    /// do not form a usable shape (not 4 points, crossing edges, not convex, or practically no area).
    /// </summary>
    public static RoadPerspective? Create(IReadOnlyList<PointF>? corners, double minArea = 1e-4)
    {
        if (corners is not { Count: 4 } || !IsValidShape(corners, minArea))
            return null;

        var perspective = new RoadPerspective(corners.ToArray());

        // Numerically sound only if the corners map back onto themselves.
        var check = perspective.ToPicture(0.5, 0.5);
        return double.IsFinite(check.X) && double.IsFinite(check.Y) ? perspective : null;
    }

    /// <summary>
    /// True when the four points form a convex quadrilateral (no crossing edges) with an area of at
    /// least <paramref name="minArea"/> (in the points' units squared).
    /// </summary>
    public static bool IsValidShape(IReadOnlyList<PointF> p, double minArea = 1e-4)
    {
        if (p.Count != 4)
            return false;

        int sign = 0;
        for (int i = 0; i < 4; i++)
        {
            PointF a = p[i], b = p[(i + 1) % 4], c = p[(i + 2) % 4];
            double cross = (b.X - a.X) * (double)(c.Y - b.Y) - (b.Y - a.Y) * (double)(c.X - b.X);

            if (Math.Abs(cross) < 1e-12)
                return false;   // three corners in a line

            int s = Math.Sign(cross);
            if (sign == 0) sign = s;
            else if (s != sign) return false;   // not convex, or edges cross
        }

        double area = 0;
        for (int i = 0; i < 4; i++)
            area += p[i].X * (double)p[(i + 1) % 4].Y - p[(i + 1) % 4].X * (double)p[i].Y;

        return Math.Abs(area) / 2 >= minArea;
    }

    /// <summary>Road position (u across, v depth: 0 far .. 1 near) -> picture point.</summary>
    public PointF ToPicture(double u, double v)
    {
        double w = _g * u + _h * v + 1;
        return new PointF((float)((_a * u + _b * v + _c) / w), (float)((_d * u + _e * v + _f) / w));
    }

    /// <summary>
    /// Picture point -> road position (u across 0..1, v depth 0 far .. 1 near). Points outside the
    /// drawn shape give values outside 0..1. Returns null for points on or beyond the horizon.
    /// </summary>
    public (double U, double V)? ToRoad(PointF point)
    {
        double[] m = _inverse;
        double x = point.X, y = point.Y;
        double w = m[6] * x + m[7] * y + m[8];

        if (Math.Abs(w) < 1e-12)
            return null;

        double u = (m[0] * x + m[1] * y + m[2]) / w;
        double v = (m[3] * x + m[4] * y + m[5]) / w;

        // Beyond the horizon the mapping flips sign: such points are not on the road.
        double back = _g * u + _h * v + 1;
        if (back <= 0 || !double.IsFinite(u) || !double.IsFinite(v))
            return null;

        return (u, v);
    }

    /// <summary>A trapezoid typical for a lane seen from a gate camera, inside <paramref name="area"/>.</summary>
    public static PointF[] DefaultCorners(RectangleF area) => new[]
    {
        new PointF(area.X + area.Width * 0.38f, area.Y + area.Height * 0.40f),   // far-left
        new PointF(area.X + area.Width * 0.62f, area.Y + area.Height * 0.40f),   // far-right
        new PointF(area.X + area.Width * 0.90f, area.Y + area.Height * 0.95f),   // near-right
        new PointF(area.X + area.Width * 0.10f, area.Y + area.Height * 0.95f)    // near-left
    };
}
