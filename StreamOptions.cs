using System.Globalization;

namespace MockRtsp;

public enum RtspTransportMode
{
    TcpAndUdp,
    TcpOnly,
    UdpOnly
}

/// <summary>The size and frame rate a camera actually sends.</summary>
public readonly record struct VideoFormat(int Width, int Height, int FrameRate)
{
    /// <summary>Bytes in one raw yuv420p frame.</summary>
    public int FrameBytes => Width * Height * 3 / 2;
}

/// <summary>
/// Every setting that changes what the mock cameras send, plus the server settings.
/// Saved to options.json by OptionsStore; unknown or missing fields fall back to these defaults.
/// </summary>
public sealed class StreamOptions
{
    // ---- Video (per camera) ----
    public bool Loop { get; set; } = true;          // at the end: start again, or hold the last frame
    public int BitrateKbps { get; set; }            // 0 = encoder default
    public int KeyframeInterval { get; set; }       // frames, 0 = one keyframe per second
    public int Width { get; set; }                  // 0 = keep the source size
    public int Height { get; set; }
    public int FrameRate { get; set; }              // 0 = keep the source rate
    public bool NoBFrames { get; set; }
    public string PublishTransport { get; set; } = "tcp";     // "tcp" or "udp"

    // ---- Motion scan: still parts of a video become skip ranges ----
    public bool AutoScanMotion { get; set; } = true;          // scan videos that have no skip file yet
    public double MotionThresholdPercent { get; set; } = 0.3; // share of the picture that must change
    public double MinStillSeconds { get; set; } = 3;          // shorter still parts keep playing
    public double MotionPaddingSeconds { get; set; } = 1;     // kept before and after movement
    public string FfmpegLogLevel { get; set; } = "warning";   // error, warning, info

    // ---- MediaMTX server (applies at next server start) ----
    public string MediaMtxLogLevel { get; set; } = "warn";    // error, warn, info, debug
    public RtspTransportMode Transports { get; set; } = RtspTransportMode.TcpAndUdp;
    public int ReadTimeoutSeconds { get; set; } = 10;
    public int WriteTimeoutSeconds { get; set; } = 10;
    public string ViewerUser { get; set; } = string.Empty;
    public string ViewerPassword { get; set; } = string.Empty;
    public string PublisherUser { get; set; } = string.Empty;
    public string PublisherPassword { get; set; } = string.Empty;

    // ---- Remembered between runs ----
    public string VideoFolder { get; set; } = string.Empty;
    public string[] RecentFolders { get; set; } = Array.Empty<string>();

    public StreamOptions Clone() => (StreamOptions)MemberwiseClone();

    /// <summary>True when every setting that only applies at server start is the same in both.</summary>
    public bool ServerSettingsEqual(StreamOptions other) =>
        MediaMtxLogLevel == other.MediaMtxLogLevel
        && Transports == other.Transports
        && ReadTimeoutSeconds == other.ReadTimeoutSeconds
        && WriteTimeoutSeconds == other.WriteTimeoutSeconds
        && ViewerUser == other.ViewerUser
        && ViewerPassword == other.ViewerPassword
        && PublisherUser == other.PublisherUser
        && PublisherPassword == other.PublisherPassword;

    /// <summary>Returns a list of problems. Empty means the options are usable.</summary>
    public List<string> Validate()
    {
        var problems = new List<string>();

        if (BitrateKbps < 0 || KeyframeInterval < 0 || FrameRate < 0 || Width < 0 || Height < 0)
            problems.Add("Numeric values cannot be negative.");

        if ((Width > 0) != (Height > 0))
            problems.Add("Width and height must both be set, or both left at 0.");

        if (Width % 2 != 0 || Height % 2 != 0)
            problems.Add("Width and height must be even numbers (H.264 requires it).");

        if (FrameRate > 120)
            problems.Add("Frame rate can be at most 120.");

        if (MotionThresholdPercent < 0.1 || MotionThresholdPercent > 25)
            problems.Add("Movement needed must be between 0.1 and 25 % of the picture.");

        if (MinStillSeconds < 1 || MotionPaddingSeconds < 0)
            problems.Add("Still parts must be at least 1 second, and the time kept around movement cannot be negative.");

        if (PublishTransport is not ("tcp" or "udp"))
            problems.Add("Publish transport must be tcp or udp.");

        if ((Transports == RtspTransportMode.UdpOnly && PublishTransport == "tcp")
            || (Transports == RtspTransportMode.TcpOnly && PublishTransport == "udp"))
            problems.Add("The publish transport must be one the server accepts (RTSP transports on the Server tab).");

        if (ReadTimeoutSeconds < 1 || WriteTimeoutSeconds < 1)
            problems.Add("Timeouts must be at least 1 second.");

        if ((ViewerUser.Length > 0) != (ViewerPassword.Length > 0))
            problems.Add("Viewer login needs both a username and a password.");

        if ((PublisherUser.Length > 0) != (PublisherPassword.Length > 0))
            problems.Add("Publisher login needs both a username and a password.");

        if (!IsValidCredential(ViewerUser) || !IsValidCredential(ViewerPassword)
            || !IsValidCredential(PublisherUser) || !IsValidCredential(PublisherPassword))
            problems.Add("Logins can only use letters, digits and ! $ ( ) * + . ; < = > [ ] ^ _ - @ # (MediaMTX rejects anything else).");

        return problems;
    }

    private const string CredentialSymbols = "!$()*+.;<=>[]^_-@#";

    /// <summary>Empty, or only the characters MediaMTX accepts in a user name or password.</summary>
    private static bool IsValidCredential(string? value) =>
        string.IsNullOrEmpty(value)
        || value.All(c => (c < 128 && char.IsLetterOrDigit(c)) || CredentialSymbols.Contains(c));

    /// <summary>The format sent for a source: the configured size and rate, or the source's own.</summary>
    public VideoFormat OutputFormat(VideoInfo source)
    {
        int width = Width > 0 ? Width : source.Width;
        int height = Height > 0 ? Height : source.Height;
        int rate = FrameRate > 0 ? FrameRate : (int)Math.Round(source.FrameRate);

        if (rate < 1 || rate > 120)
            rate = 25;

        // yuv420p and H.264 need even sizes.
        return new VideoFormat(Math.Max(2, width & ~1), Math.Max(2, height & ~1), rate);
    }

    /// <summary>
    /// Decoder stage: reads the file from a position and writes raw frames, already scaled to the
    /// output size (letterboxed if its shape differs) and resampled to the output frame rate, to standard output.
    /// </summary>
    public static List<string> DecoderArguments(string source, double startSeconds, VideoFormat format)
    {
        var inv = CultureInfo.InvariantCulture;
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin" };

        // -ss before -i seeks in the input, and is frame-accurate because the video is decoded.
        if (startSeconds > 0)
            args.AddRange(new[] { "-ss", startSeconds.ToString("0.000", inv) });

        args.AddRange(new[]
        {
            "-i", source,
            "-map", "0:v:0", "-an", "-sn", "-dn",
            "-vf", ScaleFilter(format),
            "-pix_fmt", "yuv420p",
            "-f", "rawvideo", "pipe:1"
        });

        return args;
    }

    /// <summary>
    /// Fits a video into the output size without stretching it: scaled to fit, centred, with
    /// black bars where its shape differs (a playlist can mix sizes), at the output frame rate.
    /// </summary>
    public static string ScaleFilter(VideoFormat format)
    {
        var inv = CultureInfo.InvariantCulture;
        string w = format.Width.ToString(inv), h = format.Height.ToString(inv);
        return $"scale={w}:{h}:force_original_aspect_ratio=decrease,"
               + $"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={format.FrameRate.ToString(inv)}";
    }

    /// <summary>
    /// Encoder stage: takes raw frames on standard input and publishes them to MediaMTX as H.264.
    /// It runs for as long as the camera is on, so viewers stay connected through pause and seek.
    /// </summary>
    public List<string> EncoderArguments(VideoFormat format, string url)
    {
        var inv = CultureInfo.InvariantCulture;
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", FfmpegLogLevel,
            "-f", "rawvideo", "-pix_fmt", "yuv420p",
            "-video_size", $"{format.Width.ToString(inv)}x{format.Height.ToString(inv)}",
            "-framerate", format.FrameRate.ToString(inv),
            "-i", "pipe:0",
            "-an",
            "-c:v", "libx264", "-preset", "veryfast", "-tune", "zerolatency", "-pix_fmt", "yuv420p"
        };

        if (BitrateKbps > 0)
            args.AddRange(new[] { "-b:v", BitrateKbps.ToString(inv) + "k" });

        // A viewer shows nothing until the next keyframe, so by default send one every second.
        if (KeyframeInterval > 0)
            args.AddRange(new[] { "-g", KeyframeInterval.ToString(inv) });
        else
            args.AddRange(new[] { "-force_key_frames", "expr:gte(t,n_forced*1)" });

        if (NoBFrames)
            args.AddRange(new[] { "-bf", "0" });

        args.AddRange(new[] { "-f", "rtsp", "-rtsp_transport", PublishTransport, url });
        return args;
    }
}
