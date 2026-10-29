using System.Diagnostics;

namespace MockRtsp;

/// <summary>One mock camera: its stream path, its playlist of videos, and the VLC window opened for it.</summary>
public sealed class CameraEntry
{
    private List<VideoFile> _playlist;

    public CameraEntry(string streamPath, IReadOnlyList<VideoFile> playlist, MockCamera camera)
    {
        StreamPath = streamPath;
        _playlist = playlist.ToList();
        Camera = camera;
    }

    public string StreamPath { get; }

    /// <summary>The videos the camera plays, in order. Each is shared with any other camera playing the same file.</summary>
    public IReadOnlyList<VideoFile> Playlist => _playlist;

    /// <summary>The video playing now.</summary>
    public VideoFile Video => _playlist[Math.Clamp(Camera.ItemIndex, 0, _playlist.Count - 1)];

    /// <summary>The first video: the camera's picture size, frame rate and quality come from its settings.</summary>
    public VideoFile First => _playlist[0];

    public MockCamera Camera { get; }

    /// <summary>The VLC window opened for this camera, if any. Reopened after a restart.</summary>
    public Process? Vlc { get; set; }

    /// <summary>A skip range started with Skip start, waiting for Skip end. Null for none.</summary>
    public double? PendingSkipStart { get; set; }

    /// <summary>The video the pending skip range belongs to.</summary>
    public VideoFile? PendingSkipVideo { get; set; }

    public bool Plays(VideoFile video) => _playlist.Contains(video);

    /// <summary>Replaces the playlist, and the camera's with it, keeping each video's skips.</summary>
    public void SetPlaylist(IReadOnlyList<VideoFile> playlist)
    {
        if (playlist.Count == 0)
            throw new ArgumentException("A camera needs at least one video.", nameof(playlist));

        Camera.SetPlaylist(playlist.Select(v => v.Item).ToList());
        _playlist = playlist.ToList();

        foreach (var video in _playlist)
        {
            Camera.SetSkipRanges(video.Path, video.Skips);
            Camera.SetPlates(video.Path, video.Plates);
        }

        if (PendingSkipVideo is not null && !_playlist.Contains(PendingSkipVideo))
        {
            PendingSkipStart = null;
            PendingSkipVideo = null;
        }
    }
}
