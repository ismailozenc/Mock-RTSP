using System.Drawing;

namespace Birel_Plaka;

/// <summary>A camera frame kept for a group's vehicle image; released when no longer needed.</summary>
public interface IFrameHandle
{
    /// <summary>JPEG of the frame (scaled like every vehicle image), produced off the calling thread.</summary>
    Task<byte[]?> EncodeJpegAsync();

    void Release();
}

/// <summary>A plate crop as 8-bit BGR pixels (rows of Width * 3 bytes), kept for diagnostics.</summary>
public sealed record PlateCrop(int Width, int Height, byte[] Bgr);

/// <summary>
/// One OCR read of one plate box in one frame, with everything the consensus needs: the probability of
/// every character, the plate box and whether it touches the edge of the detection area (the plate may
/// be cut off there). Immutable once created. Characters are compared through their probability vectors
/// only, so nothing here knows any country's plate format.
/// </summary>
public sealed class PlateRead
{
    public PlateRead(OcrResult ocr, float detectionConfidence, RectangleF box, bool touchesLeft, bool touchesRight,
        long time, DateTime timeUtc, object? trackKey)
    {
        if (ocr.Characters.Length != ocr.Text.Length)
            throw new ArgumentException("Every character needs its probabilities.", nameof(ocr));

        Ocr = ocr;
        DetectionConfidence = detectionConfidence;
        Box = box;
        TouchesLeft = touchesLeft;
        TouchesRight = touchesRight;
        Time = time;
        TimeUtc = timeUtc;
        TrackKey = trackKey;

        // Square roots of the probabilities, so comparing two characters is one dot product.
        _roots = ocr.Characters.Select(PlateConsensus.Roots).ToArray();

        float[] c = ocr.CharacterConfidences;
        WeakestConfidence = c.Length == 0 ? 0f : c.Min();
        MedianConfidence = Median(c);
    }

    public OcrResult Ocr { get; }
    public string Text => Ocr.Text;
    public int Length => Ocr.Text.Length;

    /// <summary>Probabilities over the OCR alphabet of character <paramref name="i"/>.</summary>
    public float[] Probabilities(int i) => Ocr.Characters[i];

    private readonly float[][] _roots;

    /// <summary>Square roots of <see cref="Probabilities"/> (for the character cost).</summary>
    internal float[] Roots(int i) => _roots[i];

    /// <summary>Top probability of character <paramref name="i"/>.</summary>
    public float Confidence(int i) => Ocr.CharacterConfidences[i];

    /// <summary>Index of character <paramref name="i"/> in the OCR alphabet.</summary>
    public int Symbol(int i)
    {
        float[] p = Ocr.Characters[i];
        int best = 0;
        for (int k = 1; k < p.Length; k++)
            if (p[k] > p[best]) best = k;
        return best;
    }

    public float DetectionConfidence { get; }

    /// <summary>Plate box in full-frame pixels.</summary>
    public RectangleF Box { get; }

    /// <summary>The box reaches the left / right edge of the detection area (or of the frame).</summary>
    public bool TouchesLeft { get; }
    public bool TouchesRight { get; }

    /// <summary>When the frame was analysed (Stopwatch ticks, same clock as the travel tracks).</summary>
    public long Time { get; }
    public DateTime TimeUtc { get; }

    /// <summary>The car this read was assigned to by position (<see cref="TravelTrack"/>); groups are keyed by it.</summary>
    public object? TrackKey { get; }

    public float WeakestConfidence { get; }
    public float MedianConfidence { get; }

    /// <summary>The plate's pixels, only kept when consensus diagnostics are on.</summary>
    public PlateCrop? Crop { get; init; }

    private static float Median(float[] values)
    {
        if (values.Length == 0)
            return 0f;
        var sorted = values.OrderBy(v => v).ToArray();
        int m = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[m] : (sorted[m - 1] + sorted[m]) / 2;
    }
}
