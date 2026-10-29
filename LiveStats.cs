namespace MockRtsp;

/// <summary>Live numbers for one camera: what it sends and who watches, main stream and substream.</summary>
public sealed record CameraStats(double FramesPerSecond, double? MainKbps, int MainViewers, double? SubKbps, int SubViewers);

/// <summary>
/// Turns MediaMTX's running byte counts into bitrates. Fed the path list about once a second
/// (MediaMtxServer.GetPathStatsAsync); the bitrate of a path is the bytes it received since the
/// previous update, over the time between them. UI thread only.
/// </summary>
public sealed class LiveStats
{
    private Dictionary<string, PathStats> _last = new(StringComparer.Ordinal);
    private Dictionary<string, double> _kbps = new(StringComparer.Ordinal);
    private DateTime _lastAt;

    public void Update(Dictionary<string, PathStats> paths, DateTime at)
    {
        var kbps = new Dictionary<string, double>(StringComparer.Ordinal);
        double seconds = (at - _lastAt).TotalSeconds;

        foreach (var pair in paths)
        {
            string name = pair.Key;
            var now = pair.Value;
            // A restarted camera starts counting again from 0: no rate until the next update.
            if (seconds > 0.2 && _last.TryGetValue(name, out var before) && now.BytesReceived >= before.BytesReceived && now.Ready)
                kbps[name] = (now.BytesReceived - before.BytesReceived) * 8 / 1000.0 / seconds;
        }

        _last = paths;
        _kbps = kbps;
        _lastAt = at;
    }

    /// <summary>Forgets everything, for example when the server stops.</summary>
    public void Clear()
    {
        _last = new Dictionary<string, PathStats>(StringComparer.Ordinal);
        _kbps = new Dictionary<string, double>(StringComparer.Ordinal);
    }

    public double? KbpsOf(string path) => _kbps.TryGetValue(path, out double v) ? v : null;

    public int ViewersOf(string path) => _last.TryGetValue(path, out var s) ? s.Viewers : 0;

    /// <summary>The numbers for a camera, with the frame rate it measured itself.</summary>
    public CameraStats For(MockCamera camera, string path)
    {
        string? sub = camera.SubstreamPath;
        return new CameraStats(
            camera.MeasuredFrameRate,
            KbpsOf(path),
            ViewersOf(path),
            sub is null ? null : KbpsOf(sub),
            sub is null ? 0 : ViewersOf(sub));
    }

    /// <summary>"25.0 fps · 2.1 Mbit/s · 1 viewer  |  sub 0.4 Mbit/s · 2 viewers".</summary>
    public static string Describe(CameraStats s, bool hasSubstream)
    {
        string text = $"{s.FramesPerSecond:0.0} fps · {Rate(s.MainKbps)} · {Viewers(s.MainViewers)}";
        if (hasSubstream)
            text += $"  |  sub {Rate(s.SubKbps)} · {Viewers(s.SubViewers)}";
        return text;
    }

    private static string Rate(double? kbps) =>
        kbps is not double k ? "-- kbit/s" : k >= 1000 ? $"{k / 1000:0.0} Mbit/s" : $"{k:0} kbit/s";

    private static string Viewers(int n) => n == 1 ? "1 viewer" : $"{n} viewers";
}
