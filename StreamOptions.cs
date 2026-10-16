using System.Globalization;

namespace MockRtsp;

public enum RtspTransportMode
{
    TcpAndUdp,
    TcpOnly,
    UdpOnly
}

/// <summary>
/// Every setting that changes what the mock camera sends. The defaults reproduce the
/// behaviour from before these options existed, so an untouched dialog changes nothing.
/// </summary>
public sealed class StreamOptions
{
    // ---- FFmpeg publisher (per camera) ----
    public bool Loop { get; set; } = true;
    public bool RealTime { get; set; } = true;
    public bool DropAudio { get; set; } = true;
    public bool ReEncode { get; set; }
    public int BitrateKbps { get; set; }            // 0 = encoder default. Re-encode only.
    public int KeyframeInterval { get; set; }       // frames, 0 = encoder default. Re-encode only.
    public int Width { get; set; }                  // 0 = keep source size. Re-encode only.
    public int Height { get; set; }
    public int FrameRate { get; set; }              // 0 = keep source rate. Re-encode only.
    public bool NoBFrames { get; set; }             // Re-encode only.
    public string PublishTransport { get; set; } = "tcp";     // "tcp" or "udp"
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

        if (ReEncode && (Width > 0) != (Height > 0))
            problems.Add("Width and height must both be set, or both left at 0.");

        if (ReEncode && (Width % 2 != 0 || Height % 2 != 0))
            problems.Add("Width and height must be even numbers (H.264 requires it).");

        if (PublishTransport is not ("tcp" or "udp"))
            problems.Add("Publish transport must be tcp or udp.");

        if ((Transports == RtspTransportMode.UdpOnly && PublishTransport == "tcp")
            || (Transports == RtspTransportMode.TcpOnly && PublishTransport == "udp"))
            problems.Add("The publish transport must be one the server accepts (RTSP transports on the MediaMTX tab).");

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

    /// <summary>Builds the FFmpeg command-line arguments for publishing one camera.</summary>
    public List<string> FfmpegArguments(string source, string url)
    {
        var inv = CultureInfo.InvariantCulture;
        var args = new List<string> { "-hide_banner", "-loglevel", FfmpegLogLevel };

        // -re paces output at real-time speed. -stream_loop must come before -i.
        if (RealTime) args.Add("-re");
        if (Loop) args.AddRange(new[] { "-stream_loop", "-1" });

        args.AddRange(new[] { "-fflags", "+genpts", "-i", source });

        if (DropAudio) args.Add("-an");

        if (ReEncode)
        {
            args.AddRange(new[] { "-c:v", "libx264", "-preset", "veryfast", "-tune", "zerolatency", "-pix_fmt", "yuv420p" });

            if (BitrateKbps > 0)
                args.AddRange(new[] { "-b:v", BitrateKbps.ToString(inv) + "k" });

            if (KeyframeInterval > 0)
                args.AddRange(new[] { "-g", KeyframeInterval.ToString(inv) });

            if (NoBFrames)
                args.AddRange(new[] { "-bf", "0" });

            if (Width > 0 && Height > 0)
                args.AddRange(new[] { "-vf", $"scale={Width.ToString(inv)}:{Height.ToString(inv)}" });

            if (FrameRate > 0)
                args.AddRange(new[] { "-r", FrameRate.ToString(inv) });
        }
        else
        {
            // Pass the video through unchanged.
            args.AddRange(new[] { "-c:v", "copy" });
        }

        if (!DropAudio)
            args.AddRange(new[] { "-c:a", "copy" });

        args.AddRange(new[] { "-f", "rtsp", "-rtsp_transport", PublishTransport, url });
        return args;
    }
}
