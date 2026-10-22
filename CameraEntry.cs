using System.Diagnostics;

namespace MockRtsp;

/// <summary>One mock camera: its stream path, the video it plays, and the VLC window opened for it.</summary>
public sealed class CameraEntry
{
    public CameraEntry(string streamPath, VideoFile video, MockCamera camera)
    {
        StreamPath = streamPath;
        Video = video;
        Camera = camera;
    }

    public string StreamPath { get; }

    /// <summary>The video, shared with any other camera playing the same file.</summary>
    public VideoFile Video { get; }

    public MockCamera Camera { get; }

    /// <summary>The VLC window opened for this camera, if any. Reopened after a restart.</summary>
    public Process? Vlc { get; set; }

    /// <summary>A skip range started with Skip start, waiting for Skip end. Null for none.</summary>
    public double? PendingSkipStart { get; set; }
}
