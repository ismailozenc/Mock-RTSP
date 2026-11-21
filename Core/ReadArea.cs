using System.Globalization;
using Birel_Plaka;

namespace MockRtsp;

/// <summary>
/// Where plates are read in a video, as Birel Plaka has it for each camera:
///   Detection  the detection area (ROI): only this part of the picture goes to the plate detector,
///              and a plate touching its left or right edge may be cut off. Null: the whole picture.
///   Perspective the road perspective: four corners (far-left, far-right, near-right, near-left) of a
///              rectangle lying on the road, so the direction of a car also uses where its plate is on
///              the road, not only its size. Null: none drawn.
/// Both are fractions of the picture (0 to 1), written in Birel Plaka's own text format, so values can
/// be copied between its camera-settings.ini (Roi=, Perspective=) and Mock RTSP:
///   Detection    "0.1,0.2,0.6,0.5"                        x, y, width, height
///   Perspective  "0.3,0.2;0.7,0.2;0.9,0.9;0.1,0.9"        x,y of the four corners
/// </summary>
public sealed class ReadArea
{
    public static ReadArea WholePicture { get; } = new(null, null);

    public RectangleF? Detection { get; }
    public IReadOnlyList<PointF>? Perspective { get; }

    /// <summary>Throws ArgumentException when the area is not inside the picture or the perspective is no proper shape.</summary>
    public ReadArea(RectangleF? detection, IReadOnlyList<PointF>? perspective)
    {
        if (detection is RectangleF d && !IsInsidePicture(d))
            throw new ArgumentException($"The detection area {FormatDetection(d)} is not inside the picture (fractions 0 to 1, width and height above 0).");
        if (perspective is not null && (perspective.Count != 4 || !RoadPerspective.IsValidShape(perspective)))
            throw new ArgumentException("The road perspective must be four corners forming a convex shape (far-left, far-right, near-right, near-left).");

        Detection = detection;
        Perspective = perspective?.ToArray();
    }

    public bool IsWholePicture => Detection is null && Perspective is null;

    public ReadArea WithDetection(RectangleF? detection) => new(detection, Perspective);

    public ReadArea WithPerspective(IReadOnlyList<PointF>? perspective) => new(Detection, perspective);

    /// <summary>The detection area in frame pixels (Birel Plaka's StreamTile.GetFrameRoi): rounded, clipped to the frame.</summary>
    public Rectangle DetectionPixels(int frameWidth, int frameHeight)
    {
        var frame = new Rectangle(0, 0, frameWidth, frameHeight);
        if (Detection is not RectangleF d)
            return frame;

        var area = new Rectangle(
            (int)Math.Round(d.X * frameWidth), (int)Math.Round(d.Y * frameHeight),
            (int)Math.Round(d.Width * frameWidth), (int)Math.Round(d.Height * frameHeight));
        area.Intersect(frame);
        return area;
    }

    /// <summary>The road perspective in frame pixels (Birel Plaka's StreamTile.GetFramePerspective), or null.</summary>
    public RoadPerspective? PerspectivePixels(int frameWidth, int frameHeight) =>
        Perspective is null
            ? null
            : RoadPerspective.Create(Perspective.Select(p => new PointF(p.X * frameWidth, p.Y * frameHeight)).ToArray(), minArea: 1);

    /// <summary>For the dialog and the log: "the whole picture, with no road perspective" and the like.</summary>
    public string Describe()
    {
        string area = Detection is RectangleF d
            ? Loc.T("a detection area of {0} % x {1} % of the picture", (d.Width * 100).ToString("0"), (d.Height * 100).ToString("0"))
            : Loc.T("the whole picture");
        return Loc.T(Perspective is null ? "{0}, with no road perspective" : "{0}, with the road perspective", area);
    }

    public string? DetectionText => Detection is RectangleF d ? FormatDetection(d) : null;

    public string? PerspectiveText => Perspective is null ? null : FormatPerspective(Perspective);

    /// <summary>True when both read the same way (compared as saved text).</summary>
    public bool SameAs(string? detectionText, string? perspectiveText) =>
        DetectionText == detectionText && PerspectiveText == perspectiveText;

    // ===================================================================== text format (Birel Plaka's RoiSettings)

    public static string FormatDetection(RectangleF r) =>
        string.Join(",", new[] { r.X, r.Y, r.Width, r.Height }.Select(Num));

    public static string FormatPerspective(IReadOnlyList<PointF> corners) =>
        string.Join(";", corners.Select(p => Num(p.X) + "," + Num(p.Y)));

    /// <summary>"x,y,width,height". Throws FormatException.</summary>
    public static RectangleF ParseDetection(string text)
    {
        var n = Numbers(text, ',');
        if (n.Length != 4)
            throw new FormatException($"'{text}' is not a detection area (x,y,width,height).");
        return new RectangleF(n[0], n[1], n[2], n[3]);
    }

    /// <summary>"x,y;x,y;x,y;x,y". Throws FormatException.</summary>
    public static PointF[] ParsePerspective(string text)
    {
        string[] points = text.Split(';');
        if (points.Length != 4)
            throw new FormatException($"'{text}' is not a road perspective (four x,y corners separated by ';').");

        return points.Select(p =>
        {
            var n = Numbers(p, ',');
            if (n.Length != 2)
                throw new FormatException($"'{text}' is not a road perspective (four x,y corners separated by ';').");
            return new PointF(n[0], n[1]);
        }).ToArray();
    }

    /// <summary>Reads saved texts (null = not set). Throws FormatException for bad text or values.</summary>
    public static ReadArea Parse(string? detection, string? perspective)
    {
        try
        {
            return new ReadArea(
                string.IsNullOrWhiteSpace(detection) ? null : ParseDetection(detection),
                string.IsNullOrWhiteSpace(perspective) ? null : ParsePerspective(perspective));
        }
        catch (ArgumentException ex)
        {
            throw new FormatException(ex.Message, ex);
        }
    }

    private static bool IsInsidePicture(RectangleF r)
    {
        const float Slack = 1e-4f;      // rounding of saved fractions
        return float.IsFinite(r.X) && float.IsFinite(r.Y) && float.IsFinite(r.Width) && float.IsFinite(r.Height)
               && r.Width > 0 && r.Height > 0
               && r.X >= -Slack && r.Y >= -Slack && r.Right <= 1 + Slack && r.Bottom <= 1 + Slack;
    }

    // The shortest text that reads back to the same float, whatever the Windows number format.
    private static string Num(float value) => value.ToString(CultureInfo.InvariantCulture);

    private static float[] Numbers(string text, char separator) =>
        text.Split(separator).Select(part =>
            float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f)
                ? f
                : throw new FormatException($"'{part.Trim()}' in '{text}' is not a number.")).ToArray();
}

/// <summary>A camera of Birel Plaka's camera-settings.ini, with its drawn detection area and road perspective (as written there).</summary>
public sealed record BirelCamera(string Section, string Name, string Url, string? Roi, string? Perspective)
{
    /// <summary>Its area as a ReadArea. A detection area reaching outside the picture is clipped to it, as Birel Plaka does. Throws FormatException.</summary>
    public ReadArea ToReadArea()
    {
        RectangleF? detection = null;
        if (!string.IsNullOrWhiteSpace(Roi))
        {
            var r = ReadArea.ParseDetection(Roi);
            r.Intersect(new RectangleF(0, 0, 1, 1));
            if (r.Width <= 0 || r.Height <= 0)
                throw new FormatException($"[{Section}] Roi={Roi} lies outside the picture.");
            detection = r;
        }

        IReadOnlyList<PointF>? perspective = null;
        if (!string.IsNullOrWhiteSpace(Perspective))
        {
            var corners = ReadArea.ParsePerspective(Perspective);
            if (!RoadPerspective.IsValidShape(corners))
                throw new FormatException($"[{Section}] Perspective={Perspective} is not a proper shape (Birel Plaka ignores it too).");
            perspective = corners;
        }

        return new ReadArea(detection, perspective);
    }

    public string Describe() =>
        $"{Name}  ({Url})  -  {Loc.T(Roi is null ? "no detection area" : "detection area")}, {Loc.T(Perspective is null ? "no road perspective" : "road perspective")}";
}

/// <summary>Reads the cameras out of Birel Plaka's camera-settings.ini (read only: the file is never written).</summary>
public static class BirelCameraSettings
{
    public const string FileName = "camera-settings.ini";

    /// <summary>The [Camera_N] sections in file order. Throws InvalidDataException when the file has none.</summary>
    public static List<BirelCamera> Read(string path)
    {
        var sections = new List<(string Name, Dictionary<string, string> Values)>();
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                sections.Add((line.Substring(1, line.Length - 2).Trim(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq > 0 && sections.Count > 0)
                sections[sections.Count - 1].Values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }

        string? Value(Dictionary<string, string> values, string key) =>
            values.TryGetValue(key, out var v) && v.Length > 0 ? v : null;

        var cameras = sections
            .Where(s => s.Name.StartsWith("Camera_", StringComparison.OrdinalIgnoreCase))
            .Select(s => new BirelCamera(s.Name, Value(s.Values, "Name") ?? s.Name, Value(s.Values, "Url") ?? "(no URL)",
                Value(s.Values, "Roi"), Value(s.Values, "Perspective")))
            .ToList();

        if (cameras.Count == 0)
            throw new InvalidDataException($"'{Path.GetFileName(path)}' has no cameras ([Camera_1], [Camera_2], ...). Choose Birel Plaka's {FileName}.");
        return cameras;
    }
}
