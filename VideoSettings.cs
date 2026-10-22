namespace MockRtsp;

/// <summary>
/// The settings a single video can have of its own: how it is sent, and how its motion scan
/// decides what to skip. A video without its own settings uses the defaults from the main
/// Settings dialog.
/// </summary>
public sealed class VideoSettings
{
    public bool Loop { get; set; } = true;
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameRate { get; set; }
    public int BitrateKbps { get; set; }
    public int KeyframeInterval { get; set; }
    public bool NoBFrames { get; set; }

    public double MotionThresholdPercent { get; set; } = 0.3;
    public double MinStillSeconds { get; set; } = 3;
    public double MotionPaddingSeconds { get; set; } = 1;

    public VideoSettings Clone() => (VideoSettings)MemberwiseClone();

    /// <summary>The video settings found in a full set of options (the defaults).</summary>
    public static VideoSettings From(StreamOptions o) => new()
    {
        Loop = o.Loop,
        Width = o.Width,
        Height = o.Height,
        FrameRate = o.FrameRate,
        BitrateKbps = o.BitrateKbps,
        KeyframeInterval = o.KeyframeInterval,
        NoBFrames = o.NoBFrames,
        MotionThresholdPercent = o.MotionThresholdPercent,
        MinStillSeconds = o.MinStillSeconds,
        MotionPaddingSeconds = o.MotionPaddingSeconds
    };

    /// <summary>Writes these settings over the matching ones in a full set of options.</summary>
    public void ApplyTo(StreamOptions o)
    {
        o.Loop = Loop;
        o.Width = Width;
        o.Height = Height;
        o.FrameRate = FrameRate;
        o.BitrateKbps = BitrateKbps;
        o.KeyframeInterval = KeyframeInterval;
        o.NoBFrames = NoBFrames;
        o.MotionThresholdPercent = MotionThresholdPercent;
        o.MinStillSeconds = MinStillSeconds;
        o.MotionPaddingSeconds = MotionPaddingSeconds;
    }

    /// <summary>Problems with these settings, checked by the same rules as the main settings.</summary>
    public List<string> Validate()
    {
        var o = new StreamOptions();
        ApplyTo(o);
        return o.Validate();
    }

    /// <summary>True when the settings that change the stream itself are the same (a camera restart is needed otherwise).</summary>
    public bool SameStream(VideoSettings other) =>
        Loop == other.Loop && Width == other.Width && Height == other.Height && FrameRate == other.FrameRate
        && BitrateKbps == other.BitrateKbps && KeyframeInterval == other.KeyframeInterval && NoBFrames == other.NoBFrames;

    /// <summary>True when the motion scan would pick the same skips from the same movement data.</summary>
    public bool SameMotion(VideoSettings other) =>
        Math.Abs(MotionThresholdPercent - other.MotionThresholdPercent) < 0.0001
        && Math.Abs(MinStillSeconds - other.MinStillSeconds) < 0.0001
        && Math.Abs(MotionPaddingSeconds - other.MotionPaddingSeconds) < 0.0001;

    /// <summary>The still parts of a scanned video under these settings. Instant: no rescan.</summary>
    public List<SkipRange> StillParts(MotionProfile motion, double durationSeconds) =>
        motion.StillParts(MotionThresholdPercent, MinStillSeconds, MotionPaddingSeconds, durationSeconds);
}
