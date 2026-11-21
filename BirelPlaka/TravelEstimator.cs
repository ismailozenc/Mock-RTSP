namespace Birel_Plaka;

public enum TravelDirection
{
    /// <summary>Not enough reads yet.</summary>
    Measuring,
    /// <summary>Coming toward the camera (its front plate is seen).</summary>
    Approaching,
    /// <summary>Moving away from the camera (its rear plate is seen).</summary>
    Leaving,
    /// <summary>Seen long enough without moving.</summary>
    Stopped,
    /// <summary>Plate size and road position contradict each other.</summary>
    Unclear
}

/// <summary>What is known about one car's movement, for display. Immutable; replaced as reads arrive.</summary>
public sealed record TravelEstimate(
    TravelDirection Direction,
    int Reads,
    double Seconds,
    double SizeChange,          // relative plate size change, e.g. +0.25 = 25 % bigger
    double? RoadStart,          // road depth 0 = far edge .. 1 = near edge (null: no perspective drawn)
    double? RoadEnd,
    bool SizeSaysApproaching,
    bool SizeSignificant,
    bool RoadSignificant)
{
    public static readonly TravelEstimate Initial = new(TravelDirection.Measuring, 0, 0, 0, null, null, false, false, false);
}

/// <summary>
/// One car followed across consecutive detections of one camera. The latest estimate can be read from
/// any thread; it is only written by the camera's detection thread.
/// </summary>
public sealed class TravelTrack
{
    private static int _nextId;

    internal TravelTrack() => Id = Interlocked.Increment(ref _nextId);

    public int Id { get; }

    private volatile TravelEstimate _estimate = TravelEstimate.Initial;
    public TravelEstimate Estimate { get => _estimate; internal set => _estimate = value; }

    internal readonly List<Observation> Observations = new();
    internal long LastSeen;          // Stopwatch ticks

    internal readonly record struct Observation(long Time, float CenterX, float BaseY, float Width, float Size, double? RoadDepth);
}

/// <summary>
/// Estimates each car's travel direction from consecutive plate detections of one camera:
///
///  1. Reads are grouped into tracks by position and size, not by plate text, so OCR slips
///     (34ABC123 / 34A8C123) of the same car stay one track.
///  2. Plate size: a car coming toward the camera gets bigger, one driving away gets smaller.
///  3. Road perspective (if drawn on the camera): the bottom middle of the plate is mapped onto the road;
///     moving toward the near edge means approaching, toward the far edge means leaving. This also works
///     when the car crosses the picture at an angle and the size hardly changes.
///
/// Both are compared between the start and the end of the track (median of the first and last three
/// reads), so a car that drives up, waits at the barrier and is read for a minute keeps its direction.
/// If both are clear and agree the result is firm; if they disagree it is "unclear".
///
/// Used by one camera's detection thread only (not thread-safe by itself).
/// </summary>
public sealed class TravelEstimator
{
    // [Travel] in camera-settings.ini (checked at start-up).

    /// <summary>Reads more than this apart belong to different passes.</summary>
    public static TimeSpan TrackTimeout => AppConfig.Travel.TrackTimeout;

    public static int MinReads => AppConfig.Travel.MinReads;
    public static TimeSpan MinDuration => AppConfig.Travel.MinDuration;
    public static TimeSpan StoppedAfter => AppConfig.Travel.StoppedAfter;

    /// <summary>Size change counted as movement (ln ratio; 0.10 ≈ 10 %).</summary>
    public static double SizeThreshold => AppConfig.Travel.SizeThreshold;
    /// <summary>Road depth change counted as movement (fraction of the drawn road length).</summary>
    public static double RoadThreshold => AppConfig.Travel.RoadThreshold;

    private const int MaxObservations = 400;

    private readonly List<TravelTrack> _tracks = new();
    private readonly long _timeoutTicks;
    private readonly double _ticksPerSecond;

    public TravelEstimator(double ticksPerSecond = 0)
    {
        _ticksPerSecond = ticksPerSecond > 0 ? ticksPerSecond : System.Diagnostics.Stopwatch.Frequency;
        _timeoutTicks = (long)(TrackTimeout.TotalSeconds * _ticksPerSecond);
    }

    /// <summary>Tracks currently being followed (for tests / diagnostics).</summary>
    public int ActiveTracks => _tracks.Count;

    /// <summary>
    /// Adds the plates found in one frame (boxes in full-frame pixels) and returns the track of each
    /// box, in the same order. <paramref name="road"/> is the camera's perspective in the same pixel
    /// coordinates, or null when none is drawn.
    /// </summary>
    public TravelTrack[] Observe(IReadOnlyList<RectangleF> boxes, long time, int frameWidth, RoadPerspective? road)
    {
        _tracks.RemoveAll(t => time - t.LastSeen > _timeoutTicks);

        var result = new TravelTrack[boxes.Count];
        var taken = new HashSet<TravelTrack>();

        // Closest box-track pairs first, so two cars in one picture do not swap tracks.
        var candidates = new List<(double Cost, int Box, TravelTrack Track)>();
        for (int i = 0; i < boxes.Count; i++)
        {
            foreach (var track in _tracks)
            {
                double? cost = MatchCost(track, boxes[i], frameWidth);
                if (cost is double c)
                    candidates.Add((c, i, track));
            }
        }

        foreach (var (_, box, track) in candidates.OrderBy(c => c.Cost))
        {
            if (result[box] != null || taken.Contains(track))
                continue;

            result[box] = track;
            taken.Add(track);
        }

        for (int i = 0; i < boxes.Count; i++)
        {
            var track = result[i];
            if (track == null)
            {
                track = new TravelTrack();
                _tracks.Add(track);
                result[i] = track;
            }

            Add(track, boxes[i], time, road);
        }

        return result;
    }

    /// <summary>How well a box continues a track (lower is better), or null when it cannot be the same car.</summary>
    private static double? MatchCost(TravelTrack track, RectangleF box, int frameWidth)
    {
        var last = track.Observations[^1];
        float size = PlateSize(box);

        double ratio = size / Math.Max(1e-3, last.Size);
        if (ratio < 0.6 || ratio > 1.67)
            return null;   // too big a jump in size between two reads

        double dx = (box.X + box.Width / 2) - last.CenterX;
        double dy = (box.Y + box.Height) - last.BaseY;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double allowed = Math.Max(2.5 * Math.Max(box.Width, last.Width), 0.08 * Math.Max(1, frameWidth));

        if (distance > allowed)
            return null;

        return distance / allowed + Math.Abs(Math.Log(ratio));
    }

    private static float PlateSize(RectangleF box) => MathF.Sqrt(Math.Max(1f, box.Width * box.Height));

    private void Add(TravelTrack track, RectangleF box, long time, RoadPerspective? road)
    {
        var basePoint = new PointF(box.X + box.Width / 2, box.Y + box.Height);   // where the plate meets the car's lower edge
        double? depth = road?.ToRoad(basePoint)?.V;

        if (track.Observations.Count >= MaxObservations)
            track.Observations.RemoveAt(3);   // keep the first reads: the start of the pass matters

        track.Observations.Add(new TravelTrack.Observation(time, basePoint.X, basePoint.Y, box.Width, PlateSize(box), depth));
        track.LastSeen = time;
        track.Estimate = Estimate(track.Observations, _ticksPerSecond);
    }

    internal static TravelEstimate Estimate(IReadOnlyList<TravelTrack.Observation> obs, double ticksPerSecond)
    {
        int n = obs.Count;
        double seconds = (obs[n - 1].Time - obs[0].Time) / ticksPerSecond;

        if (n < MinReads || seconds < MinDuration.TotalSeconds)
            return TravelEstimate.Initial with { Reads = n, Seconds = seconds };

        // Start and end of the pass: median of the first and the last three reads (robust to one bad box).
        double sizeStart = Median(obs.Take(3).Select(o => (double)o.Size));
        double sizeEnd = Median(obs.Skip(n - 3).Select(o => (double)o.Size));
        double sizeChange = Math.Log(sizeEnd / sizeStart);

        double? roadStart = MedianOrNull(obs.Take(3).Select(o => o.RoadDepth));
        double? roadEnd = MedianOrNull(obs.Skip(n - 3).Select(o => o.RoadDepth));
        double? roadChange = roadStart is double a && roadEnd is double b ? b - a : null;

        bool sizeSignificant = Math.Abs(sizeChange) >= SizeThreshold;
        bool roadSignificant = roadChange is double r && Math.Abs(r) >= RoadThreshold;

        TravelDirection direction;

        if (sizeSignificant && roadSignificant)
        {
            bool sizeApproach = sizeChange > 0;
            bool roadApproach = roadChange > 0;   // toward the near edge
            direction = sizeApproach == roadApproach
                ? (sizeApproach ? TravelDirection.Approaching : TravelDirection.Leaving)
                : TravelDirection.Unclear;
        }
        else if (roadSignificant)
        {
            direction = roadChange > 0 ? TravelDirection.Approaching : TravelDirection.Leaving;
        }
        else if (sizeSignificant)
        {
            direction = sizeChange > 0 ? TravelDirection.Approaching : TravelDirection.Leaving;
        }
        else if (seconds >= StoppedAfter.TotalSeconds)
        {
            direction = TravelDirection.Stopped;
        }
        else
        {
            direction = TravelDirection.Measuring;
        }

        return new TravelEstimate(
            direction, n, seconds, Math.Exp(sizeChange) - 1, roadStart, roadEnd,
            sizeChange > 0, sizeSignificant, roadSignificant);
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        int m = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[m] : (sorted[m - 1] + sorted[m]) / 2;
    }

    private static double? MedianOrNull(IEnumerable<double?> values)
    {
        var known = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return known.Length == 0 ? null : Median(known);
    }
}
