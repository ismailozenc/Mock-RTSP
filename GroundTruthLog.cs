using System.Drawing;
using System.Globalization;
using System.Text;

namespace MockRtsp;

/// <summary>One line of the ground-truth log: a plate that went out on a camera's stream.</summary>
public sealed record StreamedPlate(
    DateTime Utc,
    string Camera,
    string? Substream,
    string Video,
    double VideoTime,
    string Plate,
    double? X, double? Y, double? Width, double? Height,
    int? StreamX, int? StreamY, int? StreamWidth, int? StreamHeight);

/// <summary>
/// Writes the ground-truth log: one CSV line per plate, at the moment its frame is handed to the
/// encoder. One file per day, "ground-truth-YYYY-MM-DD.csv", in the ground-truth folder
/// (Documents\MockRtsp\ground-truth unless set otherwise). Compare it with what your ALPR system
/// read, with Score ALPR results in the app or any tool of your own.
/// Columns: utc_time, local_time, camera, substream, video, video_time, plate, x, y, width, height
/// (fractions of the video's picture), stream_x, stream_y, stream_width, stream_height (pixels in
/// the main stream, after scaling and black bars).
/// Safe to call from any thread.
/// </summary>
public sealed class GroundTruthLog
{
    public const string Header =
        "utc_time,local_time,camera,substream,video,video_time,plate,x,y,width,height,stream_x,stream_y,stream_width,stream_height";

    private readonly object _gate = new();

    public GroundTruthLog(string folder)
    {
        Folder = folder;
    }

    public string Folder { get; }

    /// <summary>Documents\MockRtsp\ground-truth.</summary>
    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MockRtsp", "ground-truth");

    /// <summary>The folder in the settings, or the default when none is set.</summary>
    public static string FolderFor(StreamOptions options) =>
        string.IsNullOrWhiteSpace(options.GroundTruthFolder) ? DefaultFolder : options.GroundTruthFolder;

    /// <summary>The file for a day (local date).</summary>
    public string PathFor(DateTime localDate) =>
        Path.Combine(Folder, $"ground-truth-{localDate:yyyy-MM-dd}.csv");

    /// <summary>Today's file.</summary>
    public string TodayPath => PathFor(DateTime.Now);

    /// <summary>Appends one line, starting the day's file with a header. Throws IOException if it cannot write.</summary>
    public void Write(StreamedPlate plate)
    {
        string line = ToCsvLine(plate);

        lock (_gate)
        {
            Directory.CreateDirectory(Folder);
            string path = PathFor(plate.Utc.ToLocalTime());
            bool isNew = !File.Exists(path) || new FileInfo(path).Length == 0;

            using var writer = new StreamWriter(path, append: true, new UTF8Encoding(false));
            if (isNew)
                writer.Write(Header + "\r\n");
            writer.Write(line + "\r\n");
        }
    }

    /// <summary>The line written for an event of a camera.</summary>
    public static StreamedPlate From(string camera, string? substream, PlateStreamedEventArgs e)
    {
        var p = e.Plate;
        var box = StreamRegion(p, e.Video.Info, e.Format);
        return new StreamedPlate(e.Utc, camera, substream, e.Video.Name, p.Time, p.Plate,
            p.X, p.Y, p.Width, p.Height, box?.X, box?.Y, box?.Width, box?.Height);
    }

    /// <summary>
    /// Where a plate's region lands in the streamed picture: the video is scaled to fit and
    /// centred with black bars (see StreamOptions.ScaleFilter). Null without a region.
    /// </summary>
    public static Rectangle? StreamRegion(PlateMark plate, VideoInfo source, VideoFormat format)
    {
        if (!plate.HasRegion || source.Width <= 0 || source.Height <= 0)
            return null;

        double scale = Math.Min((double)format.Width / source.Width, (double)format.Height / source.Height);
        double shownWidth = source.Width * scale, shownHeight = source.Height * scale;
        double left = (format.Width - shownWidth) / 2, top = (format.Height - shownHeight) / 2;

        return new Rectangle(
            (int)Math.Round(left + plate.X!.Value * shownWidth),
            (int)Math.Round(top + plate.Y!.Value * shownHeight),
            (int)Math.Round(plate.Width!.Value * shownWidth),
            (int)Math.Round(plate.Height!.Value * shownHeight));
    }

    public static string ToCsvLine(StreamedPlate p)
    {
        var inv = CultureInfo.InvariantCulture;
        return string.Join(",",
            p.Utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", inv),
            p.Utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", inv),
            Csv.Escape(p.Camera),
            Csv.Escape(p.Substream ?? string.Empty),
            Csv.Escape(p.Video),
            p.VideoTime.ToString("0.###", inv),
            Csv.Escape(p.Plate),
            Csv.Number(p.X), Csv.Number(p.Y), Csv.Number(p.Width), Csv.Number(p.Height),
            p.StreamX?.ToString(inv) ?? string.Empty,
            p.StreamY?.ToString(inv) ?? string.Empty,
            p.StreamWidth?.ToString(inv) ?? string.Empty,
            p.StreamHeight?.ToString(inv) ?? string.Empty);
    }

    /// <summary>Reads a ground-truth log file. Throws InvalidDataException naming a bad line.</summary>
    public static List<StreamedPlate> Read(string path)
    {
        var lines = Csv.ReadLines(File.ReadAllText(path));
        var result = new List<StreamedPlate>();
        var inv = CultureInfo.InvariantCulture;

        for (int n = 0; n < lines.Count; n++)
        {
            var f = lines[n];
            if (f.All(x => x.Trim().Length == 0) || (n == 0 && f[0] == "utc_time"))
                continue;

            if (f.Count < 7)
                throw new InvalidDataException($"{Path.GetFileName(path)} line {n + 1}: not a ground-truth line.");

            if (!DateTime.TryParse(f[0], inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc))
                throw new InvalidDataException($"{Path.GetFileName(path)} line {n + 1}: '{f[0]}' is not a time.");

            double? D(int i) => i < f.Count && double.TryParse(f[i], NumberStyles.Float, inv, out double v) ? v : null;
            int? I(int i) => i < f.Count && int.TryParse(f[i], NumberStyles.Integer, inv, out int v) ? v : null;

            result.Add(new StreamedPlate(utc, f[2], f[3].Length > 0 ? f[3] : null, f[4], D(5) ?? 0, f[6],
                D(7), D(8), D(9), D(10), I(11), I(12), I(13), I(14)));
        }

        return result;
    }
}
