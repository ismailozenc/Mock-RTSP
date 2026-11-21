using Birel_Plaka;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MockRtsp;

/// <summary>A plate box in one frame (frame pixels), its detector confidence and what the OCR read.</summary>
public sealed record PlateHit(RectangleF Box, float Confidence, OcrResult Ocr);

/// <summary>
/// Birel Plaka's plate detector and OCR (its Detector and PlateOCR), with the same models, inputs and
/// outputs, but without OpenCV: the frame comes from FFmpeg as RGB bytes and is cropped and resized
/// here, with the same bilinear sampling as OpenCV's INTER_LINEAR.
///   Detector: the whole frame stretched to the model's input (1x3xHxW, RGB, 0..1); output rows of 7
///             values (batch, x1, y1, x2, y2, class, score); boxes below the threshold are dropped.
///   OCR:      each box cropped from the frame and stretched to the plate config's size (1xHxWx3 RGB
///             bytes); outputs "plate" (slots x alphabet probabilities) and "region" (countries).
/// Not thread-safe: one reader per scan.
/// </summary>
public sealed class PlateReader : IDisposable
{
    private const string PlateOutput = "plate";
    private const string RegionOutput = "region";

    private readonly InferenceSession _detector;
    private readonly InferenceSession _ocr;
    private readonly OcrModelConfig _config;
    private readonly float _threshold;
    private readonly string _detectorInput;
    private readonly string _ocrInput;
    private readonly string[] _ocrOutputs;
    private readonly int _inputWidth;
    private readonly int _inputHeight;
    private readonly float[] _detectorPixels;
    private readonly byte[] _resized;
    private readonly byte[] _plate;

    /// <param name="threshold">Minimum box confidence, 0..1 (Birel Plaka's [Detection] DetectorThreshold).</param>
    /// <param name="threads">CPU threads for each model.</param>
    public PlateReader(string detectorPath, string ocrPath, OcrModelConfig config, float threshold, int threads)
    {
        if (threshold is <= 0f or >= 1f)
            throw new ArgumentOutOfRangeException(nameof(threshold), "The detector threshold must be between 0 and 1.");

        _config = config;
        _threshold = threshold;
        var options = new SessionOptions { IntraOpNumThreads = Math.Max(1, threads), InterOpNumThreads = 1 };

        _detector = new InferenceSession(detectorPath, options);
        try
        {
            var (name, input) = _detector.InputMetadata.First();
            int[] dims = input.Dimensions;
            if (dims.Length != 4 || dims[1] != 3 || dims[2] <= 0 || dims[3] <= 0)
                throw new InvalidDataException(
                    $"{Path.GetFileName(detectorPath)}: expected a fixed input of 1x3xHxW, the model has [{string.Join(",", dims)}].");
            _detectorInput = name;
            _inputHeight = dims[2];
            _inputWidth = dims[3];

            _ocr = new InferenceSession(ocrPath, options);
            try
            {
                (_ocrInput, _ocrOutputs) = CheckOcr(_ocr, Path.GetFileName(ocrPath), config);
            }
            catch
            {
                _ocr.Dispose();
                throw;
            }
        }
        catch
        {
            _detector.Dispose();
            throw;
        }

        _detectorPixels = new float[3 * _inputWidth * _inputHeight];
        _resized = new byte[3 * _inputWidth * _inputHeight];
        _plate = new byte[3 * config.ImageWidth * config.ImageHeight];
    }

    /// <summary>The same checks as Birel Plaka's PlateOCR: the model must match its plate config.</summary>
    private static (string Input, string[] Outputs) CheckOcr(InferenceSession session, string file, OcrModelConfig config)
    {
        var (inputName, input) = session.InputMetadata.First();
        int[] dims = input.Dimensions;
        if (dims.Length != 4 || dims[1] != config.ImageHeight || dims[2] != config.ImageWidth || dims[3] != 3)
            throw new InvalidDataException(
                $"{file}: expected an input of Nx{config.ImageHeight}x{config.ImageWidth}x3 (plate config), the model has [{string.Join(",", dims)}].");

        if (!session.OutputMetadata.TryGetValue(PlateOutput, out var plate))
            throw new InvalidDataException($"{file}: the model has no '{PlateOutput}' output.");
        int[] p = plate.Dimensions;
        if (p.Length != 3 || p[1] != config.Slots || p[2] != config.Alphabet.Length)
            throw new InvalidDataException(
                $"{file}: '{PlateOutput}' is [{string.Join(",", p)}], the plate config needs Nx{config.Slots}x{config.Alphabet.Length}.");

        bool hasRegion = session.OutputMetadata.TryGetValue(RegionOutput, out var region);
        if (config.Regions.Count > 0)
        {
            if (!hasRegion)
                throw new InvalidDataException($"{file}: the plate config lists regions, the model has no '{RegionOutput}' output.");
            int[] r = region!.Dimensions;
            if (r.Length != 2 || r[1] != config.Regions.Count)
                throw new InvalidDataException(
                    $"{file}: '{RegionOutput}' is [{string.Join(",", r)}], the plate config lists {config.Regions.Count} regions.");
        }
        else if (hasRegion)
        {
            throw new InvalidDataException($"{file}: the model has a '{RegionOutput}' output, the plate config lists no regions.");
        }

        return (inputName, config.Regions.Count > 0 ? new[] { PlateOutput, RegionOutput } : new[] { PlateOutput });
    }

    /// <summary>The plates in one frame: RGB bytes, rows of width * 3.</summary>
    public List<PlateHit> Read(byte[] rgb, int width, int height)
    {
        var hits = new List<PlateHit>();

        // Detector: the whole frame stretched to the model's input, planar RGB scaled to 0..1.
        Resize(rgb, width, 0, 0, width, height, _resized, _inputWidth, _inputHeight);
        int plane = _inputWidth * _inputHeight;
        for (int i = 0; i < plane; i++)
        {
            _detectorPixels[i] = _resized[i * 3] / 255f;
            _detectorPixels[plane + i] = _resized[i * 3 + 1] / 255f;
            _detectorPixels[2 * plane + i] = _resized[i * 3 + 2] / 255f;
        }

        var input = new DenseTensor<float>(_detectorPixels, new[] { 1, 3, _inputHeight, _inputWidth });
        float[] rows;
        using (var results = _detector.Run(new[] { NamedOnnxValue.CreateFromTensor(_detectorInput, input) }))
            rows = results.First().AsTensor<float>().ToArray();

        float scaleX = (float)width / _inputWidth;
        float scaleY = (float)height / _inputHeight;
        const int Step = 7;

        for (int i = 0; i < rows.Length / Step; i++)
        {
            int o = i * Step;
            float confidence = rows[o + 6];
            if (confidence < _threshold)
                continue;

            int x1 = Math.Max(0, Math.Min((int)(rows[o + 1] * scaleX), width - 1));
            int y1 = Math.Max(0, Math.Min((int)(rows[o + 2] * scaleY), height - 1));
            int x2 = Math.Max(0, Math.Min((int)(rows[o + 3] * scaleX), width));
            int y2 = Math.Max(0, Math.Min((int)(rows[o + 4] * scaleY), height));
            if (x2 - x1 <= 0 || y2 - y1 <= 0)
                continue;

            OcrResult ocr = ReadPlate(rgb, width, x1, y1, x2 - x1, y2 - y1);
            if (string.IsNullOrWhiteSpace(ocr.Text))
                continue;           // as in Birel Plaka: only empty reads are dropped here

            hits.Add(new PlateHit(new RectangleF(x1, y1, x2 - x1, y2 - y1), confidence, ocr));
        }

        return hits;
    }

    /// <summary>OCR of one box: cropped, stretched to the plate config's size, RGB bytes (NHWC).</summary>
    private OcrResult ReadPlate(byte[] rgb, int width, int x, int y, int w, int h)
    {
        Resize(rgb, width, x, y, w, h, _plate, _config.ImageWidth, _config.ImageHeight);
        var input = new DenseTensor<byte>(_plate, new[] { 1, _config.ImageHeight, _config.ImageWidth, 3 });

        float[]? plate = null, region = null;
        using (var results = _ocr.Run(new[] { NamedOnnxValue.CreateFromTensor(_ocrInput, input) }, _ocrOutputs))
        {
            foreach (var result in results)
            {
                if (result.Name == PlateOutput) plate = result.AsTensor<float>().ToArray();
                else if (result.Name == RegionOutput) region = result.AsTensor<float>().ToArray();
            }
        }

        if (plate is null)
            throw new InvalidDataException($"The OCR model returned no '{PlateOutput}' output.");

        return OcrResult.Decode(plate, region ?? Array.Empty<float>(), _config);
    }

    /// <summary>
    /// Bilinear resize of a rectangle of an RGB image (rows of <paramref name="srcWidth"/> * 3 bytes)
    /// to <paramref name="dw"/> x <paramref name="dh"/>, sampled like OpenCV's INTER_LINEAR: pixel
    /// centres aligned, edges clamped.
    /// </summary>
    internal static void Resize(byte[] src, int srcWidth, int x0, int y0, int w, int h, byte[] dst, int dw, int dh)
    {
        var xs = new (int I0, int I1, float A)[dw];
        double sx = (double)w / dw;
        for (int x = 0; x < dw; x++)
            xs[x] = Tap((x + 0.5) * sx - 0.5, w);

        double sy = (double)h / dh;
        int stride = srcWidth * 3;
        for (int y = 0; y < dh; y++)
        {
            var (r0, r1, ay) = Tap((y + 0.5) * sy - 0.5, h);
            int row0 = (y0 + r0) * stride, row1 = (y0 + r1) * stride;
            int o = y * dw * 3;

            for (int x = 0; x < dw; x++)
            {
                var (c0, c1, ax) = xs[x];
                int p00 = row0 + (x0 + c0) * 3, p01 = row0 + (x0 + c1) * 3;
                int p10 = row1 + (x0 + c0) * 3, p11 = row1 + (x0 + c1) * 3;

                for (int c = 0; c < 3; c++)
                {
                    float top = src[p00 + c] + (src[p01 + c] - src[p00 + c]) * ax;
                    float bottom = src[p10 + c] + (src[p11 + c] - src[p10 + c]) * ax;
                    dst[o + x * 3 + c] = (byte)Math.Clamp((int)MathF.Round(top + (bottom - top) * ay), 0, 255);
                }
            }
        }
    }

    /// <summary>The two source positions and the weight of the second, OpenCV style.</summary>
    private static (int I0, int I1, float A) Tap(double f, int size)
    {
        int i = (int)Math.Floor(f);
        float a = (float)(f - i);
        if (i < 0)
        {
            i = 0;
            a = 0;
        }

        if (i >= size - 1)
        {
            i = size - 1;
            a = 0;
        }

        return (i, Math.Min(i + 1, size - 1), a);
    }

    public void Dispose()
    {
        _ocr.Dispose();
        _detector.Dispose();
    }
}
