using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MockRtsp;

/// <summary>
/// Small pictures of a video file at a fixed interval, for the timeline's hover preview, kept as
/// JPEG images (a few KB each). Made in the background by one FFmpeg pass; they become available
/// one by one while it runs. When done they are saved next to the video in
/// "&lt;video&gt;.mockrtsp.previews", so the next time the video is used they load at once instead
/// of being made again. Any one picture can be read straight away, in or out of the file: each is
/// a separate JPEG and the file starts with a table of where each one is.
/// </summary>
public sealed class ThumbnailStrip : IDisposable
{
    private const string Magic = "MRTPREV1";

    private readonly object _gate = new();
    private readonly List<byte[]> _frames = new();
    private readonly CancellationTokenSource _cancel = new();
    private Process? _process;
    private bool _disposed;

    private ThumbnailStrip(int width, int height, double intervalSeconds, int expectedCount)
    {
        Width = width;
        Height = height;
        IntervalSeconds = intervalSeconds;
        ExpectedCount = expectedCount;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Seconds between thumbnails. Thumbnail i shows time i * IntervalSeconds.</summary>
    public double IntervalSeconds { get; }

    public int ExpectedCount { get; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _frames.Count;
            }
        }
    }

    public bool IsComplete { get; private set; }

    /// <summary>True when the pictures came from the file saved next to the video.</summary>
    public bool FromFile { get; private set; }

    /// <summary>Why making thumbnails failed, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>Where the pictures of a video are saved.</summary>
    public static string PathFor(string video) => video + ".mockrtsp.previews";

    /// <summary>Thumbnail i as a JPEG image, or null if it is not made yet.</summary>
    public byte[]? GetJpeg(int index)
    {
        lock (_gate)
        {
            return index >= 0 && index < _frames.Count ? _frames[index] : null;
        }
    }

    /// <summary>
    /// Loads the saved pictures if they are still those of this file, otherwise starts making
    /// them in the background. Returns straight away. About one per second for short videos, at
    /// most 600 for long ones.
    /// </summary>
    public static ThumbnailStrip Start(string ffmpegPath, string file, VideoInfo source, int height = 108)
    {
        double duration = Math.Max(source.DurationSeconds, 1);
        double interval = Math.Max(1, Math.Ceiling(duration / 600));
        int expected = Math.Max(1, (int)Math.Ceiling(duration / interval));

        double aspect = source.Height > 0 ? (double)source.Width / source.Height : 16.0 / 9.0;
        int width = Math.Clamp((int)Math.Round(height * aspect) & ~1, 32, 384);

        var strip = new ThumbnailStrip(width, height, interval, expected);
        _ = Task.Run(() =>
        {
            if (!strip.TryLoad(file))
                strip.Generate(ffmpegPath, file, duration);
        });
        return strip;
    }

    // ===================================================================== making them

    private void Generate(string ffmpegPath, string file, double duration)
    {
        var inv = CultureInfo.InvariantCulture;
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin" })
            psi.ArgumentList.Add(arg);

        // Long videos: decode keyframes only, which is many times faster. Short ones: every frame,
        // so each thumbnail is the exact moment.
        if (duration > 300)
        {
            psi.ArgumentList.Add("-skip_frame");
            psi.ArgumentList.Add("nokey");
        }

        // One JPEG per thumbnail, one after the other on standard output.
        foreach (var arg in new[]
        {
            "-i", file, "-map", "0:v:0", "-an", "-sn", "-dn",
            "-vf", $"fps=1/{IntervalSeconds.ToString(inv)},scale={Width.ToString(inv)}:{Height.ToString(inv)}",
            "-c:v", "mjpeg", "-q:v", "5", "-pix_fmt", "yuvj420p", "-f", "image2pipe", "pipe:1"
        })
        {
            psi.ArgumentList.Add(arg);
        }

        string lastError = string.Empty;
        var stamp = SourceStamp(file);

        try
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
            }

            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lastError = e.Data.Trim();
            };
            _process.BeginErrorReadLine();

            var output = new BufferedStream(_process.StandardOutput.BaseStream, 1 << 16);

            while (!_cancel.IsCancellationRequested)
            {
                var jpeg = ReadJpeg(output);
                if (jpeg is null)
                    break;

                lock (_gate)
                {
                    _frames.Add(jpeg);
                }
            }

            _process.WaitForExit();

            if (_cancel.IsCancellationRequested)
                return;

            if (Count == 0)
                Error = lastError.Length > 0 ? lastError : "FFmpeg made no thumbnails.";
            else if (stamp is not null)
                TrySave(file, stamp.Value);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception or ObjectDisposedException or InvalidDataException)
        {
            if (!_cancel.IsCancellationRequested)
            {
                Error = ex.Message;
                ErrorReporter.Report(ex, $"Making thumbnails for {file}");
            }
        }
        finally
        {
            IsComplete = true;
        }
    }

    /// <summary>
    /// Reads one JPEG image from a stream of them, by following its structure (a byte search for
    /// the end marker could stop early inside its tables). Null at the end of the stream.
    /// </summary>
    internal static byte[]? ReadJpeg(Stream input)
    {
        int first = input.ReadByte();
        if (first < 0)
            return null;

        var image = new MemoryStream(16 * 1024);
        if (first != 0xFF || Next(input) != 0xD8)
            throw new InvalidDataException("FFmpeg sent something that is not a JPEG image.");

        image.WriteByte(0xFF);
        image.WriteByte(0xD8);

        int marker = ReadMarker(input, image);
        while (true)
        {
            if (marker == 0xD9)                                 // end of image
                return image.ToArray();

            if (marker is (>= 0xD0 and <= 0xD7) or 0x01)       // markers without a length
            {
                marker = ReadMarker(input, image);
                continue;
            }

            int high = Next(input), low = Next(input);
            int length = (high << 8) | low;
            if (length < 2)
                throw new InvalidDataException("A JPEG image from FFmpeg is damaged.");

            image.WriteByte((byte)high);
            image.WriteByte((byte)low);
            Copy(input, image, length - 2);

            if (marker != 0xDA)                                 // not the start of the picture data
            {
                marker = ReadMarker(input, image);
                continue;
            }

            // Picture data: runs until a marker that is not a stuffed 0xFF or a restart marker.
            while (true)
            {
                int b = Next(input);
                image.WriteByte((byte)b);
                if (b != 0xFF)
                    continue;

                int code = Next(input);
                while (code == 0xFF)                            // fill bytes
                {
                    image.WriteByte(0xFF);
                    code = Next(input);
                }

                image.WriteByte((byte)code);
                if (code == 0x00 || code is >= 0xD0 and <= 0xD7)
                    continue;

                marker = code;
                break;
            }
        }
    }

    /// <summary>Reads the next marker (0xFF, then its code), writes it, returns the code.</summary>
    private static int ReadMarker(Stream input, Stream image)
    {
        int b = Next(input);
        if (b != 0xFF)
            throw new InvalidDataException("A JPEG image from FFmpeg is damaged.");

        int code = Next(input);
        while (code == 0xFF)
            code = Next(input);

        image.WriteByte(0xFF);
        image.WriteByte((byte)code);
        return code;
    }

    private static int Next(Stream input)
    {
        int b = input.ReadByte();
        return b >= 0 ? b : throw new EndOfStreamException("FFmpeg stopped in the middle of a picture.");
    }

    private static void Copy(Stream input, Stream image, int count)
    {
        var buffer = new byte[count];
        input.ReadExactly(buffer, 0, count);
        image.Write(buffer, 0, count);
    }

    // ===================================================================== the file next to the video

    // Layout (little-endian):
    //   "MRTPREV1"
    //   int64 video file size, int64 video last-write time (UTC ticks)   -- a changed video is redone
    //   int32 width, int32 height, double interval, int32 count
    //   int32 length of each picture, count times
    //   the pictures, one after the other

    /// <summary>The video's size and time, so a replaced video is not shown with old pictures.</summary>
    private static (long Size, long Ticks)? SourceStamp(string file)
    {
        try
        {
            var info = new FileInfo(file);
            return (info.Length, info.LastWriteTimeUtc.Ticks);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private bool TryLoad(string file)
    {
        string path = PathFor(file);
        try
        {
            if (!File.Exists(path) || SourceStamp(file) is not { } stamp)
                return false;

            using var reader = new BinaryReader(File.OpenRead(path), Encoding.ASCII);
            if (Encoding.ASCII.GetString(reader.ReadBytes(Magic.Length)) != Magic
                || reader.ReadInt64() != stamp.Size
                || reader.ReadInt64() != stamp.Ticks
                || reader.ReadInt32() != Width
                || reader.ReadInt32() != Height
                || Math.Abs(reader.ReadDouble() - IntervalSeconds) > 1e-9)
            {
                return false;                   // another video, or other sizes: make them again
            }

            int count = reader.ReadInt32();
            if (count <= 0 || count > ExpectedCount + 2)
                return false;

            var lengths = new int[count];
            for (int i = 0; i < count; i++)
            {
                lengths[i] = reader.ReadInt32();
                if (lengths[i] <= 0 || lengths[i] > 4 * 1024 * 1024)
                    return false;
            }

            var frames = new List<byte[]>(count);
            foreach (int length in lengths)
            {
                var jpeg = reader.ReadBytes(length);
                if (jpeg.Length != length || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
                    return false;
                frames.Add(jpeg);
            }

            lock (_gate)
            {
                _frames.AddRange(frames);
            }

            FromFile = true;
            IsComplete = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            return false;                       // unreadable: make them again, and save over it
        }
    }

    /// <summary>Saves the pictures next to the video. Quietly does nothing if the folder is read-only.</summary>
    private void TrySave(string file, (long Size, long Ticks) stamp)
    {
        string path = PathFor(file);
        string temp = path + ".tmp";

        try
        {
            List<byte[]> frames;
            lock (_gate)
            {
                frames = _frames.ToList();
            }

            using (var writer = new BinaryWriter(File.Create(temp), Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes(Magic));
                writer.Write(stamp.Size);
                writer.Write(stamp.Ticks);
                writer.Write(Width);
                writer.Write(Height);
                writer.Write(IntervalSeconds);
                writer.Write(frames.Count);
                foreach (var frame in frames)
                    writer.Write(frame.Length);
                foreach (var frame in frames)
                    writer.Write(frame);
            }

            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not worth an error: the pictures are made again next time.
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Stops making thumbnails. Ones already made stay available.</summary>
    public void Dispose()
    {
        Process? process;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            process = _process;
        }

        _cancel.Cancel();

        try
        {
            if (process is not null && !process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already gone.
        }
    }
}
