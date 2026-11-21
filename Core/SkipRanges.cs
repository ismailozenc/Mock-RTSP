namespace MockRtsp;

/// <summary>A part of a video, in seconds, that the camera jumps over. End is exclusive.</summary>
public sealed record SkipRange(double Start, double End)
{
    public double Length => End - Start;

    public bool Contains(double seconds) => seconds >= Start && seconds < End;
}

/// <summary>
/// The rules for skip ranges: tidying a list, and deciding where the camera goes from a
/// position. Saving them next to the video is SkipRangeFile's job.
/// </summary>
public static class SkipRanges
{
    /// <summary>Shortest range kept. Anything shorter is dropped.</summary>
    public const double MinLength = 0.2;


    /// <summary>
    /// Sorted, inside the video, at least MinLength long, with overlapping or touching ranges
    /// merged. Every list the camera and the file see goes through this.
    /// </summary>
    public static List<SkipRange> Normalize(IEnumerable<SkipRange> ranges, double durationSeconds)
    {
        double end = durationSeconds > 0 ? durationSeconds : double.MaxValue;

        var sorted = ranges
            .Select(r => r.Start <= r.End ? r : new SkipRange(r.End, r.Start))
            .Select(r => new SkipRange(Math.Max(0, r.Start), Math.Min(end, r.End)))
            .Where(r => r.Length >= MinLength)
            .OrderBy(r => r.Start)
            .ToList();

        var merged = new List<SkipRange>();
        foreach (var range in sorted)
        {
            int last = merged.Count - 1;
            if (last >= 0 && range.Start <= merged[last].End + 0.001)
                merged[last] = merged[last] with { End = Math.Max(merged[last].End, range.End) };
            else
                merged.Add(range);
        }

        return merged;
    }

    /// <summary>True if the ranges leave at least half a second of video to play.</summary>
    public static bool LeavesSomethingToPlay(IReadOnlyList<SkipRange> ranges, double durationSeconds) =>
        durationSeconds <= 0 || durationSeconds - ranges.Sum(r => r.Length) >= 0.5;

    /// <summary>What the camera should do at a position.</summary>
    public enum Decision
    {
        Play,           // not in a skip range
        JumpTo,         // jump to Target
        EndReached      // the rest of a video that does not loop is skipped
    }

    /// <summary>
    /// Decides where to go from a position: past the skip range it is in (and past any range
    /// that starts right there), wrapping to the start when looping.
    /// </summary>
    public static (Decision Decision, double Target) Resolve(IReadOnlyList<SkipRange> ranges, double position, double durationSeconds, bool loop)
    {
        if (ranges.Count == 0 || !LeavesSomethingToPlay(ranges, durationSeconds))
            return (Decision.Play, position);

        var inside = ranges.FirstOrDefault(r => r.Contains(position));
        if (inside is null)
            return (Decision.Play, position);

        double target = inside.End;
        bool atEnd = durationSeconds > 0 && target >= durationSeconds - 0.05;

        if (!atEnd)
            return (Decision.JumpTo, target);

        if (!loop)
            return (Decision.EndReached, inside.Start);

        // Wrap to the first moment that is not skipped.
        var atStart = ranges.FirstOrDefault(r => r.Contains(0));
        return (Decision.JumpTo, atStart?.End ?? 0);
    }
}
