namespace MockRtsp;

/// <summary>
/// How the gaps (still parts, skipped when streaming) of a video are picked from its motion data:
/// a sample with at least <see cref="ThresholdPercent"/> of the picture changing is movement,
/// <see cref="PaddingSeconds"/> either side of it are kept, and a still part is a gap only when it
/// lasts at least <see cref="MinStillSeconds"/>. Each video keeps the settings its gaps were
/// picked with (so the RTSP app can draw the threshold); the scanner's defaults are used for a
/// video scanned for the first time.
/// </summary>
public sealed record MotionSettings(double ThresholdPercent, double MinStillSeconds, double PaddingSeconds)
{
    /// <summary>
    /// What Mock RTSP used before the scanner had its own settings (its old defaults). Only for
    /// files from then that have motion data but do not say which settings picked their gaps.
    /// </summary>
    public static MotionSettings Legacy { get; } = new(0.3, 3, 1);

    public List<SkipRange> StillParts(MotionProfile motion, double durationSeconds) =>
        motion.StillParts(ThresholdPercent, MinStillSeconds, PaddingSeconds, durationSeconds);

    /// <summary>The problems with these values (empty when they can be used).</summary>
    public List<string> Validate()
    {
        var problems = new List<string>();
        if (!(ThresholdPercent >= 0.1 && ThresholdPercent <= 25))
            problems.Add("Movement needed must be between 0.1 and 25 % of the picture.");
        if (!(MinStillSeconds >= 1 && MinStillSeconds <= 3600))
            problems.Add("Gaps (still parts) must be at least 1 second and at most 3600.");
        if (!(PaddingSeconds >= 0 && PaddingSeconds <= 60))
            problems.Add("The time kept around movement must be 0 to 60 seconds.");
        return problems;
    }

    public string Describe() =>
        $"movement over {ThresholdPercent:0.0} % of the picture, gaps of at least {MinStillSeconds:0.#} s, {PaddingSeconds:0.#} s kept around movement";
}
