using System.Globalization;
using System.Text;

namespace MockRtsp;

/// <summary>Which way a vehicle drives, seen from the camera.</summary>
public enum VehicleDirection
{
    Coming,     // towards the camera
    Going       // away from the camera
}

/// <summary>
/// Ground truth for vehicle detection: one vehicle in a video, at a moment in a playing part,
/// with its direction and its plate (empty when it cannot be read or does not matter). It belongs
/// to whichever playing part contains its time, so labels follow the parts when skips change.
/// </summary>
public sealed record VehicleMark(double Time, VehicleDirection Direction, string Plate)
{
    public string DirectionText => Direction == VehicleDirection.Coming ? "coming" : "going";
}

/// <summary>A part of a video that is streamed: between two skips (or the start or end). Number counts from 1.</summary>
public sealed record PlayingPart(int Number, double Start, double End)
{
    public double Length => End - Start;

    public double Middle => (Start + End) / 2;

    /// <summary>Start inclusive; the last part also includes the very end of the video.</summary>
    public bool Contains(double seconds, double durationSeconds) =>
        seconds >= Start - 0.0005 && (seconds < End || (End >= durationSeconds - 0.0005 && seconds <= End + 0.0005));
}

/// <summary>What one playing part holds: its vehicles, and whether it was marked as having none.</summary>
public sealed record PartLabels(PlayingPart Part, IReadOnlyList<VehicleMark> Vehicles, bool MarkedEmpty)
{
    /// <summary>Labelled: it has vehicles, or it was checked and has none.</summary>
    public bool IsLabelled => Vehicles.Count > 0 || MarkedEmpty;
}

/// <summary>Counts for a video: parts, how many are labelled, and the vehicles.</summary>
public sealed record LabelSummary(int Parts, int Labelled, int Vehicles, int Coming, int Going, int Outside)
{
    public int ToLabel => Parts - Labelled;

    /// <summary>"12 parts, 7 labelled (9 vehicles: 5 coming, 4 going), 5 to label".</summary>
    public string Describe()
    {
        var text = new StringBuilder($"{Parts} playing part{(Parts == 1 ? "" : "s")}, {Labelled} labelled");
        if (Vehicles > 0)
            text.Append($" ({Vehicles} vehicle{(Vehicles == 1 ? "" : "s")}: {Coming} coming, {Going} going)");
        if (ToLabel > 0)
            text.Append($", {ToLabel} to label");
        if (Outside > 0)
            text.Append($"; {Outside} label{(Outside == 1 ? "" : "s")} now in skipped parts");
        return text.ToString();
    }
}

/// <summary>
/// The rules for vehicle labels: the playing parts of a video, which labels belong to which part,
/// the counts, tidying, and the CSV export.
/// </summary>
public static class VehicleLabels
{
    /// <summary>
    /// The parts that play: the video minus its skips. With no skips, or skips that would leave
    /// nothing to play (the camera ignores those), the whole video is one part.
    /// </summary>
    public static List<PlayingPart> PlayingParts(IReadOnlyList<SkipRange> skips, double durationSeconds)
    {
        var parts = new List<PlayingPart>();
        if (durationSeconds <= 0)
            return parts;

        if (skips.Count == 0 || !SkipRanges.LeavesSomethingToPlay(skips, durationSeconds))
        {
            parts.Add(new PlayingPart(1, 0, durationSeconds));
            return parts;
        }

        double from = 0;
        foreach (var skip in skips.OrderBy(s => s.Start))
        {
            if (skip.Start - from >= 0.05)
                parts.Add(new PlayingPart(parts.Count + 1, from, skip.Start));
            from = Math.Max(from, skip.End);
        }

        if (durationSeconds - from >= 0.05)
            parts.Add(new PlayingPart(parts.Count + 1, from, durationSeconds));

        return parts;
    }

    /// <summary>Each playing part with the labels inside it, in order.</summary>
    public static List<PartLabels> Group(IReadOnlyList<PlayingPart> parts, IReadOnlyList<VehicleMark> vehicles,
        IReadOnlyList<double> emptyParts, double durationSeconds) =>
        parts.Select(part => new PartLabels(
                part,
                vehicles.Where(v => part.Contains(v.Time, durationSeconds)).OrderBy(v => v.Time).ToList(),
                emptyParts.Any(t => part.Contains(t, durationSeconds))))
            .ToList();

    /// <summary>Labels that are in no playing part any more (the skips changed after labelling).</summary>
    public static int CountOutside(IReadOnlyList<PlayingPart> parts, IReadOnlyList<VehicleMark> vehicles,
        IReadOnlyList<double> emptyParts, double durationSeconds) =>
        vehicles.Count(v => !parts.Any(p => p.Contains(v.Time, durationSeconds)))
        + emptyParts.Count(t => !parts.Any(p => p.Contains(t, durationSeconds)));

    public static LabelSummary Summarize(IReadOnlyList<SkipRange> skips, IReadOnlyList<VehicleMark> vehicles,
        IReadOnlyList<double> emptyParts, double durationSeconds) =>
        Summarize(PlayingParts(skips, durationSeconds), vehicles, emptyParts, durationSeconds);

    public static LabelSummary Summarize(IReadOnlyList<PlayingPart> parts, IReadOnlyList<VehicleMark> vehicles,
        IReadOnlyList<double> emptyParts, double durationSeconds)
    {
        var groups = Group(parts, vehicles, emptyParts, durationSeconds);
        var inside = groups.SelectMany(g => g.Vehicles).ToList();
        return new LabelSummary(parts.Count, groups.Count(g => g.IsLabelled), inside.Count,
            inside.Count(v => v.Direction == VehicleDirection.Coming), inside.Count(v => v.Direction == VehicleDirection.Going),
            CountOutside(parts, vehicles, emptyParts, durationSeconds));
    }

    /// <summary>Sorted by time, inside the video, plates trimmed.</summary>
    public static List<VehicleMark> Normalize(IEnumerable<VehicleMark> vehicles, double durationSeconds) =>
        vehicles
            .Select(v => v with
            {
                Plate = (v.Plate ?? string.Empty).Trim(),
                Time = Math.Max(0, durationSeconds > 0 ? Math.Min(v.Time, durationSeconds) : v.Time)
            })
            .OrderBy(v => v.Time)
            .ThenBy(v => v.Direction)
            .ThenBy(v => v.Plate, StringComparer.Ordinal)
            .ToList();

    /// <summary>Sorted, inside the video, one per moment.</summary>
    public static List<double> NormalizeEmpty(IEnumerable<double> times, double durationSeconds) =>
        times
            .Select(t => Math.Max(0, durationSeconds > 0 ? Math.Min(t, durationSeconds) : t))
            .Select(t => Math.Round(t, 3))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

    /// <summary>"coming SBFN79E, going (no plate)" for a part, or "no vehicles" / "not labelled".</summary>
    public static string Describe(PartLabels group) =>
        group.Vehicles.Count > 0
            ? string.Join(", ", group.Vehicles.Select(v => v.DirectionText + " " + (v.Plate.Length > 0 ? v.Plate : "(no plate)")))
            : group.MarkedEmpty ? "no vehicles" : "not labelled";

    public static bool TryParseDirection(string? text, out VehicleDirection direction)
    {
        switch ((text ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "coming":
            case "c":
            case "in":
            case "towards":
                direction = VehicleDirection.Coming;
                return true;
            case "going":
            case "g":
            case "out":
            case "away":
                direction = VehicleDirection.Going;
                return true;
            default:
                direction = VehicleDirection.Coming;
                return false;
        }
    }

    // ===================================================================== CSV

    /// <summary>
    /// One line per vehicle, with its part: part, part_start, part_end, time, direction, plate.
    /// A part marked as having no vehicles gets one line with direction "none".
    /// </summary>
    public static string ToCsv(IReadOnlyList<PartLabels> groups)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("part,part_start,part_end,time,direction,plate\r\n");
        foreach (var group in groups)
        {
            string part = $"{group.Part.Number},{group.Part.Start.ToString("0.###", inv)},{group.Part.End.ToString("0.###", inv)}";
            foreach (var v in group.Vehicles)
                sb.Append(part).Append(',').Append(v.Time.ToString("0.###", inv)).Append(',')
                  .Append(v.DirectionText).Append(',').Append(Csv.Escape(v.Plate)).Append("\r\n");

            if (group.Vehicles.Count == 0 && group.MarkedEmpty)
                sb.Append(part).Append(",,none,\r\n");
        }

        return sb.ToString();
    }
}
