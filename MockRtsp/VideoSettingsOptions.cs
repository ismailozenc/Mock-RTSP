namespace MockRtsp;

/// <summary>A video's own stream settings next to the RTSP app's defaults (StreamOptions).</summary>
public static class VideoSettingsOptions
{
    /// <summary>The video settings found in a full set of options (the defaults).</summary>
    public static VideoSettings From(StreamOptions o) => new()
    {
        Loop = o.Loop,
        Width = o.Width,
        Height = o.Height,
        FrameRate = o.FrameRate,
        BitrateKbps = o.BitrateKbps,
        KeyframeInterval = o.KeyframeInterval,
        NoBFrames = o.NoBFrames
    };

    /// <summary>Writes these settings over the matching ones in a full set of options.</summary>
    public static void ApplyTo(this VideoSettings s, StreamOptions o)
    {
        o.Loop = s.Loop;
        o.Width = s.Width;
        o.Height = s.Height;
        o.FrameRate = s.FrameRate;
        o.BitrateKbps = s.BitrateKbps;
        o.KeyframeInterval = s.KeyframeInterval;
        o.NoBFrames = s.NoBFrames;
    }

    /// <summary>Problems with these settings, checked by the same rules as the main settings.</summary>
    public static List<string> Validate(this VideoSettings s)
    {
        var o = new StreamOptions();
        s.ApplyTo(o);
        return o.Validate();
    }
}
