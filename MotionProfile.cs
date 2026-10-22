namespace MockRtsp;

/// <summary>
/// How much of the picture changes over time: one level per sample, the share of pixels that
/// changed since the previous sample. Level = percent x 10, capped at 255 (25.5 %), so
/// 0.5 % is 5 and 2 % is 20.
/// </summary>
public sealed record MotionProfile(double IntervalSeconds, byte[] Levels)
{
    public double DurationSeconds => Levels.Length * IntervalSeconds;

    public double PercentAt(int index) => Levels[index] / 10.0;

    /// <summary>
    /// The still parts of the video as skip ranges. A sample with at least thresholdPercent of
    /// the picture changing counts as movement; paddingSeconds either side of it are kept; a
    /// remaining still part is skipped only if it lasts at least minStillSeconds.
    /// No movement at all gives no skips, so a quiet video still plays.
    /// </summary>
    public List<SkipRange> StillParts(double thresholdPercent, double minStillSeconds, double paddingSeconds, double durationSeconds)
    {
        double duration = durationSeconds > 0 ? durationSeconds : DurationSeconds;
        byte level = (byte)Math.Min(255, Math.Max(1, Math.Round(thresholdPercent * 10)));

        // Times that must play: each moving sample, widened by the padding.
        var keep = new List<(double Start, double End)>();
        for (int i = 0; i < Levels.Length; i++)
        {
            if (Levels[i] < level)
                continue;

            double start = Math.Max(0, i * IntervalSeconds - paddingSeconds);
            double end = Math.Min(duration, (i + 1) * IntervalSeconds + paddingSeconds);

            int last = keep.Count - 1;
            if (last >= 0 && start <= keep[last].End)
                keep[last] = (keep[last].Start, Math.Max(keep[last].End, end));
            else
                keep.Add((start, end));
        }

        if (keep.Count == 0)
            return new List<SkipRange>();

        // Everything between the kept parts is still.
        var still = new List<SkipRange>();
        double cursor = 0;
        foreach (var (start, end) in keep)
        {
            if (start - cursor >= minStillSeconds)
                still.Add(new SkipRange(cursor, start));
            cursor = end;
        }

        if (duration - cursor >= minStillSeconds)
            still.Add(new SkipRange(cursor, duration));

        return SkipRanges.Normalize(still, duration);
    }
}
