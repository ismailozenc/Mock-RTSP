namespace MockRtsp;

/// <summary>
/// The stream settings a single video can have of its own in the RTSP app: how it is sent. A video
/// without its own settings uses the RTSP app's defaults. (How its gaps are picked from motion is
/// the scanner's business: <see cref="MotionSettings"/>.)
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

    public VideoSettings Clone() => (VideoSettings)MemberwiseClone();

    /// <summary>True when the settings that change the stream itself are the same (a camera restart is needed otherwise).</summary>
    public bool SameStream(VideoSettings other) =>
        Loop == other.Loop && Width == other.Width && Height == other.Height && FrameRate == other.FrameRate
        && BitrateKbps == other.BitrateKbps && KeyframeInterval == other.KeyframeInterval && NoBFrames == other.NoBFrames;
}
