using System.Globalization;
using System.Text;

namespace MockRtsp;

/// <summary>One plate an ALPR system read, from its results file.</summary>
public sealed record PlateRead(DateTime Utc, string Plate, string? Camera, int Line);

/// <summary>What happened to one streamed plate.</summary>
public enum PlateOutcome
{
    Hit,        // read correctly in the time window
    Close,      // read with one character wrong
    Missed      // not read
}

public sealed record ScoredPlate(StreamedPlate Truth, PlateOutcome Outcome, PlateRead? Read, double? LatencySeconds);

public sealed record ScoreResult(
    IReadOnlyList<ScoredPlate> Plates,
    IReadOnlyList<PlateRead> UnmatchedReads,
    double BeforeSeconds,
    double AfterSeconds)
{
    public int Hits => Plates.Count(p => p.Outcome == PlateOutcome.Hit);
    public int Close => Plates.Count(p => p.Outcome == PlateOutcome.Close);
    public int Missed => Plates.Count(p => p.Outcome == PlateOutcome.Missed);
    public double HitRate => Plates.Count == 0 ? 0 : (double)Hits / Plates.Count;
}

/// <summary>
/// Scores an ALPR system against the ground-truth log: which streamed plates it read, how
/// quickly, which it missed, and what it read that was not there.
///
/// A plate counts as read when the results have the same plate (letters and digits only, any
/// case) from the same camera, between BeforeSeconds before and AfterSeconds after it was
/// streamed (or until the next time the same plate is streamed, whichever is sooner). Latency is from the moment it was streamed to the first such read (negative when
/// the system read it before the marked moment, for example as the car approached). Further
/// reads of the same plate in the window are repeats, not errors. A read one character off is
/// reported as "close" and does not count as a hit.
///
/// Results file: CSV with a header. Columns found by name:
///   time    "time", "timestamp", "utc", "utc_time", "datetime" or "date": ISO 8601 (with Z or an
///           offset; without one it is local time) or Unix time in seconds or milliseconds
///   plate   "plate", "plate_text", "text", "number" or "license_plate"
///   camera  optional: "camera", "camera_path", "path", "channel", "stream" or "url"; matches
///           when it ends with the camera's path or its substream's (".../101", "101", a full URL)
/// </summary>
public static class PlateScorer
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] TimeColumns = { "time", "timestamp", "utc", "utc_time", "datetime", "date" };
    private static readonly string[] PlateColumns = { "plate", "plate_text", "text", "number", "license_plate", "licence_plate" };
    private static readonly string[] CameraColumns = { "camera", "camera_path", "path", "channel", "stream", "url" };

    public static ScoreResult Score(IReadOnlyList<StreamedPlate> truth, IReadOnlyList<PlateRead> reads, double beforeSeconds = 2, double afterSeconds = 10)
    {
        var used = new HashSet<PlateRead>();
        var scored = new List<ScoredPlate>();

        var ordered = truth.OrderBy(t => t.Utc).ToList();
        for (int k = 0; k < ordered.Count; k++)
        {
            var t = ordered[k];
            string plate = PlateMark.Normalize(t.Plate);

            // The same plate streamed again later (a loop, another pass) gets its own reads.
            var again = ordered.Skip(k + 1).FirstOrDefault(o => o.Camera == t.Camera && PlateMark.Normalize(o.Plate) == plate);
            DateTime repeatsUntil = again is null ? DateTime.MaxValue : again.Utc.AddSeconds(-beforeSeconds);

            // Reads after the next pass of the same plate starts belong to that pass.
            var windowEnd = t.Utc.AddSeconds(afterSeconds);
            if (again is not null && repeatsUntil < windowEnd)
                windowEnd = repeatsUntil;

            var window = reads
                .Where(r => !used.Contains(r) && SameCamera(r.Camera, t))
                .Where(r => (r.Utc - t.Utc).TotalSeconds >= -beforeSeconds && r.Utc <= windowEnd)
                .OrderBy(r => r.Utc)
                .ToList();

            var exact = window.Where(r => PlateMark.Normalize(r.Plate) == plate).ToList();
            if (exact.Count > 0)
            {
                used.Add(exact[0]);
                foreach (var r in exact.Where(r => r.Utc < repeatsUntil))
                    used.Add(r);                                // repeats of a read plate are not errors

                scored.Add(new ScoredPlate(t, PlateOutcome.Hit, exact[0], (exact[0].Utc - t.Utc).TotalSeconds));
                continue;
            }

            var close = window.FirstOrDefault(r => OneOff(PlateMark.Normalize(r.Plate), plate));
            if (close is not null)
            {
                foreach (var r in window.Where(r => PlateMark.Normalize(r.Plate) == PlateMark.Normalize(close.Plate)))
                    used.Add(r);

                scored.Add(new ScoredPlate(t, PlateOutcome.Close, close, (close.Utc - t.Utc).TotalSeconds));
                continue;
            }

            scored.Add(new ScoredPlate(t, PlateOutcome.Missed, null, null));
        }

        var unmatched = reads.Where(r => !used.Contains(r)).OrderBy(r => r.Utc).ToList();
        return new ScoreResult(scored, unmatched, beforeSeconds, afterSeconds);
    }

    /// <summary>True when the read has no camera, or it ends with the plate's camera path or substream path.</summary>
    private static bool SameCamera(string? readCamera, StreamedPlate truth)
    {
        if (string.IsNullOrWhiteSpace(readCamera))
            return true;

        string camera = readCamera.Trim().TrimEnd('/');
        return Matches(camera, truth.Camera) || (truth.Substream is { } sub && Matches(camera, sub));

        static bool Matches(string read, string path) =>
            read.EndsWith(path, StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/" + read, StringComparison.OrdinalIgnoreCase)
            || path.Equals(read, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One character changed, added or removed.</summary>
    private static bool OneOff(string a, string b)
    {
        if (a == b || Math.Abs(a.Length - b.Length) > 1 || a.Length == 0 || b.Length == 0)
            return false;

        int i = 0, j = 0, edits = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j])
            {
                i++;
                j++;
                continue;
            }

            if (++edits > 1)
                return false;

            if (a.Length > b.Length) i++;
            else if (b.Length > a.Length) j++;
            else { i++; j++; }
        }

        return edits + (a.Length - i) + (b.Length - j) <= 1;
    }

    // ===================================================================== reading results

    /// <summary>Reads an ALPR results file. Throws InvalidDataException saying what is wrong.</summary>
    public static List<PlateRead> ReadResults(string path)
    {
        var lines = Csv.ReadLines(File.ReadAllText(path));
        if (lines.Count == 0)
            throw new InvalidDataException($"{Path.GetFileName(path)} is empty.");

        var header = lines[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int time = header.FindIndex(TimeColumns.Contains);
        int plate = header.FindIndex(PlateColumns.Contains);
        int camera = header.FindIndex(CameraColumns.Contains);

        if (time < 0 || plate < 0)
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} needs a header line with a time column ({string.Join(", ", TimeColumns)}) " +
                $"and a plate column ({string.Join(", ", PlateColumns)}). Found: {string.Join(", ", header)}.");

        var reads = new List<PlateRead>();
        for (int n = 1; n < lines.Count; n++)
        {
            var f = lines[n];
            if (f.All(x => x.Trim().Length == 0))
                continue;

            if (Math.Max(time, plate) >= f.Count)
                throw new InvalidDataException($"{Path.GetFileName(path)} line {n + 1}: too few columns.");

            if (!TryParseInstant(f[time], out var utc))
                throw new InvalidDataException($"{Path.GetFileName(path)} line {n + 1}: '{f[time]}' is not a time.");

            string text = f[plate].Trim();
            if (text.Length == 0)
                continue;                                   // a detection without a reading

            reads.Add(new PlateRead(utc, text, camera >= 0 && camera < f.Count ? f[camera].Trim() : null, n + 1));
        }

        return reads;
    }

    /// <summary>ISO 8601 (Z or offset; none = local time), or Unix time in seconds or milliseconds.</summary>
    public static bool TryParseInstant(string text, out DateTime utc)
    {
        text = text.Trim();
        var inv = CultureInfo.InvariantCulture;

        if (double.TryParse(text, NumberStyles.Float, inv, out double number) && number > 100_000_000)
        {
            utc = number > 100_000_000_000
                ? UnixEpoch.AddMilliseconds(number)
                : UnixEpoch.AddSeconds(number);
            return true;
        }

        if (DateTimeOffset.TryParse(text, inv, DateTimeStyles.AssumeLocal, out var instant))
        {
            utc = instant.UtcDateTime;
            return true;
        }

        utc = default;
        return false;
    }

    // ===================================================================== reports

    /// <summary>A plain text summary for the screen.</summary>
    public static string Summary(ScoreResult r)
    {
        var sb = new StringBuilder();
        int total = r.Plates.Count;
        sb.AppendLine($"Plates streamed: {total}");
        if (total == 0)
        {
            sb.AppendLine("Nothing to score: the ground-truth log has no plates in it.");
            return sb.ToString();
        }

        sb.AppendLine($"Read correctly:  {r.Hits} ({r.HitRate:P1})");
        sb.AppendLine($"One char off:    {r.Close}");
        sb.AppendLine($"Missed:          {r.Missed}");
        sb.AppendLine($"Reads of plates not streamed (or outside the window): {r.UnmatchedReads.Count}");
        sb.AppendLine($"Window: {r.BeforeSeconds:0.#} s before to {r.AfterSeconds:0.#} s after each plate was streamed.");

        var latencies = r.Plates.Where(p => p.Outcome == PlateOutcome.Hit).Select(p => p.LatencySeconds!.Value).OrderBy(x => x).ToList();
        if (latencies.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Latency, streamed to first correct read:");
            sb.AppendLine($"  average {latencies.Average():0.000} s, median {Percentile(latencies, 0.5):0.000} s, "
                          + $"95% {Percentile(latencies, 0.95):0.000} s, fastest {latencies[0]:0.000} s, slowest {latencies[latencies.Count - 1]:0.000} s");
        }

        var cameras = r.Plates.GroupBy(p => p.Truth.Camera).ToList();
        if (cameras.Count > 1)
        {
            sb.AppendLine();
            sb.AppendLine("Per camera:");
            foreach (var g in cameras.OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                int hits = g.Count(p => p.Outcome == PlateOutcome.Hit);
                sb.AppendLine($"  {g.Key}: {hits} of {g.Count()} read ({(double)hits / g.Count():P1}), {g.Count(p => p.Outcome == PlateOutcome.Missed)} missed");
            }
        }

        var problems = r.Plates.Where(p => p.Outcome != PlateOutcome.Hit).ToList();
        if (problems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Not read correctly:");
            foreach (var p in problems.Take(50))
            {
                sb.Append($"  {p.Truth.Utc.ToLocalTime():HH:mm:ss.fff}  {p.Truth.Camera}  {p.Truth.Plate}  ({p.Truth.Video} at {PlateMarks.FormatTime(p.Truth.VideoTime)})");
                sb.AppendLine(p.Outcome == PlateOutcome.Close ? $"  read as {p.Read!.Plate}" : "  missed");
            }

            if (problems.Count > 50)
                sb.AppendLine($"  ... and {problems.Count - 50} more (see the saved report).");
        }

        return sb.ToString();
    }

    /// <summary>One line per streamed plate, for a spreadsheet.</summary>
    public static string ToCsv(ScoreResult r)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("utc_time,camera,video,video_time,plate,outcome,read_as,read_time,latency_ms\r\n");
        foreach (var p in r.Plates)
        {
            sb.Append(string.Join(",",
                p.Truth.Utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", inv),
                Csv.Escape(p.Truth.Camera),
                Csv.Escape(p.Truth.Video),
                p.Truth.VideoTime.ToString("0.###", inv),
                Csv.Escape(p.Truth.Plate),
                p.Outcome.ToString().ToLowerInvariant(),
                Csv.Escape(p.Read?.Plate ?? string.Empty),
                p.Read?.Utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", inv) ?? string.Empty,
                p.LatencySeconds is double l ? Math.Round(l * 1000).ToString(inv) : string.Empty)).Append("\r\n");
        }

        return sb.ToString();
    }

    private static double Percentile(List<double> sorted, double p)
    {
        double index = p * (sorted.Count - 1);
        int low = (int)Math.Floor(index), high = (int)Math.Ceiling(index);
        return sorted[low] + (sorted[high] - sorted[low]) * (index - low);
    }
}
