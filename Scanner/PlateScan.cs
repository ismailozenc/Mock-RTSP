using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Birel_Plaka;

namespace MockRtsp;

/// <summary>
/// The settings of a plate scan: Birel Plaka's factory values (defaults\camera-settings.ini of the
/// copied version, see BirelPlaka\SOURCE.txt), so a scan reads plates the way Birel Plaka's cameras
/// do. Plate-format and country checks are left out: the scan collects every plate it can read and
/// the person labelling picks the right one.
/// </summary>
internal sealed record PlateScanSettings(
    float DetectorThreshold,        // [Detection] DetectorThreshold
    float MinDetection,             // [Confidence] MinDetection
    int IntervalMs,                 // [Detection] IntervalMs: video time between analysed frames
    TravelOptions Travel,           // [Travel]
    ConsensusRules Rules,           // [Consensus] MinReads, MinShare, MinCover, DissentReads, DissentFraction
    double GroupMaxSeconds,         // [Consensus] GroupMaxSeconds
    double JoinMaxCost,             // [Consensus] JoinMaxCost
    int EdgeMarginPixels,           // [Consensus] EdgeMarginPixels
    int PhotoCandidates,            // [Consensus] PhotoCandidates
    int MaxFrameWidth)              // wider videos are scanned scaled down to this width
{
    public static PlateScanSettings BirelPlakaFactory { get; } = new(
        DetectorThreshold: 0.50f,
        MinDetection: 0.60f,
        IntervalMs: 100,
        Travel: new TravelOptions(TimeSpan.FromSeconds(3), 3, TimeSpan.FromSeconds(0.4), TimeSpan.FromSeconds(1.5), 0.10, 0.06),
        Rules: new ConsensusRules(MinReads: 5, MinShare: 0.72, MinCover: 2, DissentReads: 2, DissentFraction: 0.25),
        GroupMaxSeconds: 10,
        JoinMaxCost: 2.5,
        EdgeMarginPixels: 2,
        PhotoCandidates: 2,
        MaxFrameWidth: 1920);

    public const string DetectorFile = "yolo-v9-t-384-license-plates-end2end.onnx";
    public const string OcrFile = "cct_xs_v2_global.onnx";
    public const string OcrConfigFile = "cct_xs_v2_global_plate_config.yaml";
}

/// <summary>One distinct text read for a car: how often, and the best OCR and box confidence.</summary>
internal sealed record PlateReading(string Text, int Count, double BestOcr, double BestDetection);

/// <summary>
/// One car found by a plate scan: its plate (voted by Birel Plaka's consensus when the reads clearly
/// agree, otherwise the best guess and why), every distinct reading, the direction from its plate's
/// movement (Birel Plaka's estimate, and the most likely one when that cannot tell), and JPEG crops of
/// the plate and the car around it.
/// </summary>
internal sealed record PlateCar(
    double Start,
    double End,
    double Time,                // the moment of the best read: the vehicle's label time
    bool Decided,
    string Plate,
    string? Reason,
    string Movement,            // coming, going, stopped, unclear or unknown (Birel Plaka's estimate)
    string? LikelyMovement,     // when Movement is neither coming nor going: the more likely of the two, or null (see DirectionGuess)
    int Reads,
    int Votes,
    double? Agreement,          // lowest share of the vote of any character (1 = all reads agree)
    IReadOnlyList<PlateReading> Readings,
    byte[]? PlateJpeg,
    byte[]? CarJpeg)
{
    /// <summary>The direction for a label, when Birel Plaka's estimate says coming or going.</summary>
    [JsonIgnore]
    public VehicleDirection? Direction => ParseMovement(Movement);

    /// <summary>The most likely direction: Birel Plaka's estimate, else the guess from the same measurements; null when nothing tells.</summary>
    [JsonIgnore]
    public VehicleDirection? MostLikelyDirection => Direction ?? ParseMovement(LikelyMovement);

    /// <summary>For the cars list: "coming", or "going? (unclear)" for a guess.</summary>
    [JsonIgnore]
    public string DirectionText => Direction is not null || LikelyMovement is null ? Movement : $"{LikelyMovement}? ({Movement})";

    private static VehicleDirection? ParseMovement(string? movement) => movement switch
    {
        "coming" => VehicleDirection.Coming,
        "going" => VehicleDirection.Going,
        _ => null
    };
}

/// <summary>
/// The most likely direction of a car when Birel Plaka's travel estimate says neither coming nor going.
/// Birel Plaka only says so when the plate's size (or its position on the drawn road) changes by at
/// least [Travel] SizeThreshold (RoadThreshold); a smaller change still points one way (the clearer of
/// the two, each against its threshold). When size and road clearly disagree ("unclear") the plate's
/// size is taken: Birel Plaka then asks to check the drawn road perspective, so the size is the surer
/// of the two. Coming: the plate grows (moves to the near edge of the road); going: it shrinks.
/// </summary>
internal static class DirectionGuess
{
    /// <summary>Below this change of plate size (log ratio, about 3 %) nothing is guessed: box noise.</summary>
    public const double MinSizeChange = 0.03;

    /// <summary>Below this change of road depth (0 far .. 1 near) nothing is guessed.</summary>
    public const double MinRoadChange = 0.02;

    /// <param name="estimate">Birel Plaka's estimate for the car's track, or null.</param>
    /// <param name="reads">The car's reads: time and plate box.</param>
    /// <returns>"coming", "going", or null when nothing tells.</returns>
    public static string? Likely(TravelEstimate? estimate, IReadOnlyList<(long Time, RectangleF Box)> reads, TravelOptions travel)
    {
        switch (estimate?.Direction)
        {
            case TravelDirection.Approaching:
                return "coming";
            case TravelDirection.Leaving:
                return "going";
            case TravelDirection.Unclear when estimate.SizeChange != 0:
                return estimate.SizeChange > 0 ? "coming" : "going";
        }

        // Birel Plaka's own measurements, when it made them (enough reads): size and road, each against its threshold.
        double size = 0, road = 0;
        if (estimate is { } e && e.Reads >= travel.MinReads && e.Seconds >= travel.MinDuration.TotalSeconds && e.SizeChange > -1)
        {
            size = Math.Log(1 + e.SizeChange);
            if (e.RoadStart is double a && e.RoadEnd is double b)
                road = b - a;
        }
        else
        {
            // Too few reads for an estimate: the plate's size from the first to the last reads.
            var ordered = reads.OrderBy(r => r.Time).ToList();
            if (ordered.Count < 2)
                return null;
            int k = Math.Min(3, ordered.Count / 2);
            double Size(RectangleF box) => Math.Sqrt(Math.Max(1, box.Width * box.Height));
            double first = Median(ordered.Take(k).Select(r => Size(r.Box)));
            double last = Median(ordered.Skip(ordered.Count - k).Select(r => Size(r.Box)));
            size = Math.Log(last / first);
        }

        bool sizeTells = Math.Abs(size) >= MinSizeChange;
        bool roadTells = Math.Abs(road) >= MinRoadChange;
        double sign = (sizeTells, roadTells) switch
        {
            (true, true) => Math.Abs(size) / travel.SizeThreshold >= Math.Abs(road) / travel.RoadThreshold ? size : road,
            (true, false) => size,
            (false, true) => road,
            _ => 0
        };

        return sign > 0 ? "coming" : sign < 0 ? "going" : null;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        int m = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[m] : (sorted[m - 1] + sorted[m]) / 2;
    }
}

internal sealed record ScannedPart(double Start, double End, int Frames);

/// <summary>The result of scanning a video's playing parts, saved next to it (<see cref="PlateScanFile"/>).</summary>
internal sealed record PlateScan(
    DateTime ScannedUtc,
    string Detector,
    string Ocr,
    int IntervalMs,
    string? DetectionArea,          // the ReadArea used (Birel Plaka's text format); null: whole picture / none
    string? RoadPerspective,
    IReadOnlyList<ScannedPart> Parts,
    IReadOnlyList<PlateCar> Cars,
    IReadOnlyList<double>? ToCheck = null)    // moments in parts prefilled from this reading and not looked at yet
{
    /// <summary>The cars of one playing part (those whose best read is inside it).</summary>
    public List<PlateCar> CarsIn(PlayingPart part, double durationSeconds) =>
        Cars.Where(c => part.Contains(c.Time, durationSeconds)).OrderBy(c => c.Time).ToList();
}

/// <summary>"&lt;video&gt;.mockrtsp.plates.json": the latest plate scan of a video (crops inside, base64).</summary>
internal static class PlateScanFile
{
    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string PathFor(string videoPath) => videoPath + ".mockrtsp.plates.json";

    /// <summary>The saved scan, or null when there is none. Throws InvalidDataException when the file cannot be read.</summary>
    public static PlateScan? Load(string videoPath)
    {
        string path = PathFor(videoPath);
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PlateScan>(File.ReadAllText(path), JsonSettings)
                   ?? throw new InvalidDataException("the file is empty");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' could not be read: {ex.Message}", ex);
        }
    }

    /// <summary>Saves atomically (written beside, then swapped in).</summary>
    public static void Save(string videoPath, PlateScan scan)
    {
        string path = PathFor(videoPath);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(scan, JsonSettings));
        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }
}

/// <summary>
/// Reads the plates in a video's playing parts with Birel Plaka's models and logic: FFmpeg decodes
/// each part at <see cref="PlateScanSettings.IntervalMs"/> (like a camera analysing a frame every
/// IntervalMs), <see cref="PlateReader"/> finds and reads the plates, <see cref="TravelEstimator"/>
/// follows each plate across frames as one car (and tells its direction), and <see cref="ReadGroups"/>
/// votes each car's reads into one plate. Each part is scanned on its own. Runs on the caller's
/// thread (call it from a background task); not thread-safe.
/// </summary>
internal sealed class PlateScanner : IDisposable
{
    private const double TicksPerSecond = 1000;    // read times are video milliseconds

    private readonly string _ffmpeg;
    private readonly PlateScanSettings _settings;
    private readonly OcrModelConfig _ocrConfig;
    private readonly PlateReader _reader;

    /// <summary>Loads the models from <paramref name="modelsFolder"/>. Throws when they are missing or do not match.</summary>
    public PlateScanner(string ffmpegPath, string modelsFolder, PlateScanSettings settings)
    {
        _ffmpeg = ffmpegPath;
        _settings = settings;

        string detector = Path.Combine(modelsFolder, PlateScanSettings.DetectorFile);
        string ocr = Path.Combine(modelsFolder, PlateScanSettings.OcrFile);
        string config = Path.Combine(modelsFolder, PlateScanSettings.OcrConfigFile);
        foreach (var file in new[] { detector, ocr, config })
        {
            if (!File.Exists(file))
                throw new FileNotFoundException($"The plate model file is missing: {file}", file);
        }

        _ocrConfig = OcrModelConfig.Load(config);
        _reader = new PlateReader(detector, ocr, _ocrConfig, settings.DetectorThreshold,
            Math.Max(1, Environment.ProcessorCount / 2));
    }

    /// <summary>
    /// Scans the parts, reading plates only inside the area's detection area and using its road
    /// perspective for the direction, as Birel Plaka does with a camera's. <paramref name="progress"/>
    /// gets the share done (0 to 1) and a line about it. Throws OperationCanceledException when
    /// cancelled (the parts done so far have been reported to <paramref name="partDone"/>).
    /// <paramref name="partDone"/> gets each part as soon as it is read, with its cars (on this thread).
    /// </summary>
    public PlateScan Scan(string videoPath, VideoInfo info, IReadOnlyList<PlayingPart> parts, ReadArea area,
        Action<double, string> progress, Action<ScannedPart, IReadOnlyList<PlateCar>> partDone, CancellationToken cancel)
    {
        AppConfig.Travel = _settings.Travel;           // what the copied TravelEstimator reads

        var policy = new GroupPolicy(
            _settings.Rules,
            PassGapSeconds: _settings.Travel.TrackTimeout.TotalSeconds,
            MaxSeconds: _settings.GroupMaxSeconds,
            JoinMaxCost: _settings.JoinMaxCost,
            PhotoCandidates: _settings.PhotoCandidates,
            MinDetection: () => _settings.MinDetection,
            FormatCheck: _ => null,
            RegionCheck: null);

        var (width, height) = FrameSize(info);
        Rectangle detection = area.DetectionPixels(width, height);
        RoadPerspective? perspective = area.PerspectivePixels(width, height);
        if (detection.Width <= 0 || detection.Height <= 0)
            throw new InvalidOperationException("The detection area is empty at this picture size.");
        double fps = 1000.0 / _settings.IntervalMs;
        double total = Math.Max(1e-6, parts.Sum(p => p.Length));
        double done = 0;
        var cars = new List<PlateCar>();
        var scanned = new List<ScannedPart>();

        foreach (var part in parts)
        {
            cancel.ThrowIfCancellationRequested();
            var travel = new TravelEstimator(TicksPerSecond);
            var groups = new ReadGroups(policy, _ocrConfig.Alphabet, TicksPerSecond);
            int frames = 0;
            int firstCar = cars.Count;          // this part's cars are the ones added from here
            string where = $"part {part.Number} of {parts.Count}";

            foreach (var (rgb, seconds) in Frames(videoPath, part, width, height, fps, cancel))
            {
                frames++;
                long time = (long)Math.Round(seconds * TicksPerSecond);
                var closed = groups.CloseExpired(time);

                var hits = _reader.Read(rgb, width, height, detection);
                if (hits.Count > 0)
                {
                    var tracks = travel.Observe(hits.Select(h => h.Box).ToArray(), time, width, perspective);
                    for (int i = 0; i < hits.Count; i++)
                    {
                        // Edges of the detection area, as in Birel Plaka (the frame's edges are edges of the area too).
                        var hit = hits[i];
                        var read = new Birel_Plaka.PlateRead(hit.Ocr, hit.Confidence, hit.Box,
                            touchesLeft: hit.Box.X - detection.X <= _settings.EdgeMarginPixels,
                            touchesRight: hit.Box.Right >= detection.Right - _settings.EdgeMarginPixels,
                            time, DateTime.UnixEpoch.AddMilliseconds(time), tracks[i]);
                        var (_, closedNow) = groups.Add(read, () => new RgbFrame((byte[])rgb.Clone(), width, height));
                        closed.AddRange(closedNow);
                    }
                }

                cars.AddRange(closed.Select(ToCar));
                progress((done + Math.Min(part.Length, seconds - part.Start)) / total, $"{where}: {cars.Count} car(s) so far");
            }

            cars.AddRange(groups.CloseAll().Select(ToCar));
            var scannedPart = new ScannedPart(part.Start, part.End, frames);
            scanned.Add(scannedPart);
            partDone(scannedPart, cars.GetRange(firstCar, cars.Count - firstCar).OrderBy(c => c.Time).ToList());
            done += part.Length;
            progress(done / total, $"{where} done: {cars.Count} car(s) so far");
        }

        return new PlateScan(DateTime.UtcNow, PlateScanSettings.DetectorFile, PlateScanSettings.OcrFile, _settings.IntervalMs,
            area.DetectionText, area.PerspectiveText, scanned, cars.OrderBy(c => c.Time).ToList());
    }

    /// <summary>The video's size, scaled down to MaxFrameWidth; even width and height for FFmpeg.</summary>
    private (int Width, int Height) FrameSize(VideoInfo info)
    {
        int width = Math.Min(info.Width, _settings.MaxFrameWidth) / 2 * 2;
        int height = (int)Math.Round(info.Height * (double)width / info.Width) / 2 * 2;
        return (Math.Max(2, width), Math.Max(2, height));
    }

    /// <summary>The frames of one part, every 1/fps seconds, as RGB bytes with their time in the video.</summary>
    private IEnumerable<(byte[] Rgb, double Seconds)> Frames(string video, PlayingPart part, int width, int height, double fps,
        CancellationToken cancel)
    {
        var inv = CultureInfo.InvariantCulture;
        var psi = new ProcessStartInfo(_ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[]
                 {
                     "-hide_banner", "-loglevel", "error", "-nostdin",
                     "-ss", part.Start.ToString("0.000", inv), "-i", video,
                     "-t", part.Length.ToString("0.000", inv),
                     "-map", "0:v:0", "-an", "-sn", "-dn",
                     "-vf", $"fps={fps.ToString("0.###", inv)},scale={width}:{height}",
                     "-pix_fmt", "rgb24", "-f", "rawvideo", "pipe:1"
                 })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
        var errors = new List<string>();
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                lock (errors) errors.Add(e.Data.Trim());
        };
        process.BeginErrorReadLine();

        try
        {
            var stream = process.StandardOutput.BaseStream;
            int frameBytes = width * height * 3;
            for (int n = 0; ; n++)
            {
                cancel.ThrowIfCancellationRequested();
                var rgb = new byte[frameBytes];
                int got = 0;
                while (got < frameBytes)
                {
                    int read = stream.Read(rgb, got, frameBytes - got);
                    if (read <= 0)
                        break;
                    got += read;
                }

                if (got < frameBytes)
                    break;

                yield return (rgb, part.Start + n / fps);
            }

            process.WaitForExit();
            lock (errors)
            {
                if (process.ExitCode != 0 && errors.Count > 0)
                    throw new InvalidOperationException($"FFmpeg could not read part {part.Number}: {string.Join(" ", errors.TakeLast(2))}");
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
        }
    }

    private PlateCar ToCar(GroupView view)
    {
        try
        {
            var reads = view.Reads.Select(r => r.Read).ToList();
            var readings = view.Reads
                .Where(r => r.Read.Text.Length > 0)
                .GroupBy(r => r.Read.Text)
                .Select(g => new PlateReading(g.Key, g.Count(),
                    Math.Round(g.Max(r => r.Read.Ocr.Confidence), 3),
                    Math.Round(g.Max(r => r.Read.DetectionConfidence), 3)))
                .OrderByDescending(r => r.Count)
                .ThenByDescending(r => r.BestOcr)
                .ToList();

            var best = view.BestRead ?? reads.OrderByDescending(r => r.WeakestConfidence).First();
            var estimate = (view.TrackKey as TravelTrack)?.Estimate;
            string movement = estimate?.Direction switch
            {
                TravelDirection.Approaching => "coming",
                TravelDirection.Leaving => "going",
                TravelDirection.Stopped => "stopped",
                TravelDirection.Unclear => "unclear",
                _ => "unknown"
            };

            byte[]? plateJpeg = null, carJpeg = null;
            if (view.Photo is RgbFrame photo)
            {
                plateJpeg = photo.Jpeg(Grow(best.Box, 0.15f, 0.35f), maxWidth: 360);
                carJpeg = photo.Jpeg(Grow(best.Box, 2.5f, 5f), maxWidth: 480);
            }

            return new PlateCar(
                Seconds(reads.Min(r => r.Time)),
                Seconds(reads.Max(r => r.Time)),
                Seconds(best.Time),
                view.State == GroupState.Decided,
                view.Text.Length > 0 ? view.Text : readings.FirstOrDefault()?.Text ?? string.Empty,
                view.State == GroupState.Decided ? null : Reason(view),
                movement,
                movement is "coming" or "going" ? null : DirectionGuess.Likely(estimate, reads.Select(r => (r.Time, r.Box)).ToList(), _settings.Travel),
                view.Reads.Count,
                view.Votes,
                view.Consensus is { } c ? Math.Round(c.LowestShare, 3) : null,
                readings,
                plateJpeg,
                carJpeg);
        }
        finally
        {
            view.Photo?.Release();
        }
    }

    /// <summary>Why a car's plate was not decided, in the words of Birel Plaka's ConsensusText.</summary>
    private string Reason(GroupView view) => view.Reason switch
    {
        UndecidedReason.TooFewReads => $"Too few reads ({view.Votes}, at least {_settings.Rules.MinReads} needed)",
        UndecidedReason.NothingRead => "No characters could be read",
        UndecidedReason.CutOff => "Plate may be cut off: most reads touch the edge of the detection area",
        UndecidedReason.TooFewCover => $"Character {view.ReasonPosition} was seen by too few reads",
        UndecidedReason.Disagree => $"Reads disagree on character {view.ReasonPosition}",
        UndecidedReason.Dissent => $"Several reads back a different answer for character {view.ReasonPosition}",
        UndecidedReason.DissentingReads => "Several reads did not fit this plate (another car on the same spot?)",
        _ => view.Reason.ToString()
    };

    private static double Seconds(long ticks) => Math.Round(ticks / TicksPerSecond, 3);

    /// <summary>The box grown by a share of its width / height on every side.</summary>
    private static RectangleF Grow(RectangleF box, float x, float y) =>
        RectangleF.FromLTRB(box.Left - box.Width * x, box.Top - box.Height * y, box.Right + box.Width * x, box.Bottom + box.Height * y);

    public void Dispose() => _reader.Dispose();

    /// <summary>A frame kept as a car's photo candidate (RGB bytes).</summary>
    private sealed class RgbFrame : IFrameHandle
    {
        private byte[]? _rgb;
        private readonly int _width, _height;

        public RgbFrame(byte[] rgb, int width, int height)
        {
            _rgb = rgb;
            _width = width;
            _height = height;
        }

        public Task<byte[]?> EncodeJpegAsync() => Task.FromResult(Jpeg(new RectangleF(0, 0, _width, _height), _width));

        public void Release() => _rgb = null;

        /// <summary>A JPEG of an area of the frame (clamped to it), at most maxWidth wide, or null when nothing is left.</summary>
        public byte[]? Jpeg(RectangleF area, int maxWidth)
        {
            if (_rgb is not { } rgb)
                return null;

            int x0 = Math.Max(0, (int)area.Left), y0 = Math.Max(0, (int)area.Top);
            int x1 = Math.Min(_width, (int)Math.Ceiling(area.Right)), y1 = Math.Min(_height, (int)Math.Ceiling(area.Bottom));
            int w = x1 - x0, h = y1 - y0;
            if (w <= 1 || h <= 1)
                return null;

            using var bitmap = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                var row = new byte[w * 3];
                for (int y = 0; y < h; y++)
                {
                    int src = ((y0 + y) * _width + x0) * 3;
                    for (int x = 0; x < w; x++)
                    {
                        row[x * 3] = rgb[src + x * 3 + 2];        // GDI+ rows are BGR
                        row[x * 3 + 1] = rgb[src + x * 3 + 1];
                        row[x * 3 + 2] = rgb[src + x * 3];
                    }

                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, w * 3);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            // Small enough to keep many cars in the results file, sharp enough to read the plate.
            using Bitmap picture = w <= maxWidth ? (Bitmap)bitmap.Clone() : Scaled(bitmap, maxWidth, Math.Max(1, (int)Math.Round(h * (double)maxWidth / w)));

            using var stream = new MemoryStream();
            var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
            picture.Save(stream, codec, parameters);
            return stream.ToArray();
        }

        private static Bitmap Scaled(Bitmap source, int width, int height)
        {
            var scaled = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(scaled);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, width, height));
            return scaled;
        }
    }
}
