using System.Globalization;
using System.Text;

namespace MockRtsp;

/// <summary>
/// A licence plate that is visible in a video at a moment: the ground truth an ALPR system
/// should read. The region is optional, as fractions (0 to 1) of the video's own picture, so it
/// stays right whatever size the camera sends.
/// </summary>
public sealed record PlateMark(double Time, string Plate, double? X = null, double? Y = null, double? Width = null, double? Height = null)
{
    public bool HasRegion => X is not null && Y is not null && Width is not null && Height is not null;

    /// <summary>The plate as compared when scoring: upper case, letters and digits only.</summary>
    public static string Normalize(string plate) =>
        new(plate.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}

/// <summary>
/// Plate lists: tidying, checking, and the CSV format used to import and export them.
/// CSV columns: time, plate, x, y, width, height. Time is in seconds ("83.4") or minutes and
/// seconds ("1:23.4"); the region columns may be left empty. A header line is optional.
/// </summary>
public static class PlateMarks
{
    /// <summary>Sorted by time, trimmed, empty plates dropped.</summary>
    public static List<PlateMark> Normalize(IEnumerable<PlateMark> plates, double durationSeconds) =>
        plates
            .Select(p => p with { Plate = p.Plate.Trim(), Time = Math.Max(0, durationSeconds > 0 ? Math.Min(p.Time, durationSeconds) : p.Time) })
            .Where(p => p.Plate.Length > 0)
            .OrderBy(p => p.Time)
            .ThenBy(p => p.Plate, StringComparer.Ordinal)
            .ToList();

    /// <summary>Problems with one plate, or null when it is fine.</summary>
    public static string? Check(PlateMark plate, double durationSeconds)
    {
        if (plate.Plate.Trim().Length == 0)
            return "the plate text is empty";

        if (plate.Time < 0 || (durationSeconds > 0 && plate.Time > durationSeconds + 0.001))
            return $"the time {FormatTime(plate.Time)} is outside the video (0 to {FormatTime(durationSeconds)})";

        var region = new[] { plate.X, plate.Y, plate.Width, plate.Height };
        if (region.Any(v => v is null) && region.Any(v => v is not null))
            return "give all four region values (x, y, width, height) or none";

        if (plate.HasRegion)
        {
            if (region.Any(v => v < 0 || v > 1) || plate.Width <= 0 || plate.Height <= 0)
                return "region values are fractions of the picture, from 0 to 1, with width and height above 0";

            if (plate.X + plate.Width > 1.0001 || plate.Y + plate.Height > 1.0001)
                return "the region goes outside the picture";
        }

        return null;
    }

    /// <summary>"1:23.4" style, or "1:02:03.4" past an hour.</summary>
    public static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, Math.Round(seconds, 1)));
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss\.f", CultureInfo.InvariantCulture) : time.ToString(@"m\:ss\.f", CultureInfo.InvariantCulture);
    }

    /// <summary>Reads seconds ("83.4") or minutes and seconds ("1:23.4", "0:01:23.4").</summary>
    public static bool TryParseTime(string text, out double seconds)
    {
        seconds = 0;
        text = text.Trim();
        if (text.Length == 0)
            return false;

        var parts = text.Split(':');
        if (parts.Length > 3)
            return false;

        double total = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
                return false;

            // Minutes and hours are whole numbers; seconds below 60 once minutes are given.
            if (i < parts.Length - 1 && value != Math.Floor(value))
                return false;
            if (i > 0 && value >= 60)
                return false;

            total = total * 60 + value;
        }

        seconds = total;
        return true;
    }

    // ===================================================================== CSV

    public static string ToCsv(IEnumerable<PlateMark> plates)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("time,plate,x,y,width,height\r\n");
        foreach (var p in plates)
        {
            sb.Append(p.Time.ToString("0.###", inv)).Append(',')
              .Append(Csv.Escape(p.Plate)).Append(',')
              .Append(Csv.Number(p.X)).Append(',')
              .Append(Csv.Number(p.Y)).Append(',')
              .Append(Csv.Number(p.Width)).Append(',')
              .Append(Csv.Number(p.Height)).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>Reads a plate list. Throws InvalidDataException naming the line that is wrong.</summary>
    public static List<PlateMark> FromCsv(string text, double durationSeconds)
    {
        var plates = new List<PlateMark>();
        var lines = Csv.ReadLines(text);

        for (int n = 0; n < lines.Count; n++)
        {
            var fields = lines[n];
            if (fields.All(f => f.Trim().Length == 0))
                continue;

            if (n == 0 && fields[0].Trim().Equals("time", StringComparison.OrdinalIgnoreCase))
                continue;                               // header

            if (fields.Count < 2)
                throw new InvalidDataException($"Line {n + 1}: expected at least time and plate.");

            if (!TryParseTime(fields[0], out double time))
                throw new InvalidDataException($"Line {n + 1}: '{fields[0]}' is not a time (use 83.4 or 1:23.4).");

            double? Get(int index)
            {
                if (index >= fields.Count || fields[index].Trim().Length == 0)
                    return null;

                return double.TryParse(fields[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                    ? v
                    : throw new InvalidDataException($"Line {n + 1}: '{fields[index]}' is not a number.");
            }

            var plate = new PlateMark(time, fields[1].Trim(), Get(2), Get(3), Get(4), Get(5));
            if (Check(plate, durationSeconds) is { } problem)
                throw new InvalidDataException($"Line {n + 1}: {problem}.");

            plates.Add(plate);
        }

        return Normalize(plates, durationSeconds);
    }
}

/// <summary>Small CSV helpers: comma separated, double quotes around fields that need them.</summary>
public static class Csv
{
    public static string Escape(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    public static string Number(double? value, string format = "0.####") =>
        value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Splits CSV text into lines of fields, handling quoted fields with commas, quotes and line breaks.</summary>
    public static List<List<string>> ReadLines(string text)
    {
        var lines = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                fields.Add(field.ToString());
                field.Clear();
                lines.Add(fields);
                fields = new List<string>();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            lines.Add(fields);
        }

        return lines;
    }
}
