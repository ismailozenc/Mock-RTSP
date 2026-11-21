namespace Birel_Plaka;

/// <summary>
/// Everything the OCR model said about one plate crop.
/// </summary>
/// <param name="Text">The characters read (argmax per slot, end-of-plate slots dropped).</param>
/// <param name="Confidence">Mean of the characters' top probabilities (0 when nothing was read).</param>
/// <param name="Characters">Per kept character: the model's probability over the whole alphabet
/// (pad character included), in alphabet order. Same length as <paramref name="Text"/>.</param>
/// <param name="Regions">The model's country probabilities (labels in <see cref="OcrModelConfig.Regions"/>),
/// or null when the model has no region output.</param>
/// <param name="MidPadding">An end-of-plate slot was followed by more characters: the read is unreliable
/// (it still votes in the consensus, the flag is kept for diagnostics).</param>
public sealed record OcrResult(
    string Text,
    float Confidence,
    float[][] Characters,
    float[]? Regions,
    bool MidPadding)
{
    /// <summary>Top probability of each character (its confidence), same order as the text.</summary>
    public float[] CharacterConfidences { get; } = Characters.Select(p => p.Max()).ToArray();

    /// <summary>
    /// Decodes the model's "plate" output of one crop: <paramref name="slots"/> probability vectors over
    /// <see cref="OcrModelConfig.Alphabet"/>. Each slot's most likely character is kept unless it is the
    /// pad (end-of-plate) character. Unlike stopping at the first pad, a pad between characters does not
    /// cut the plate: the characters after it are kept and the read is flagged as unreliable.
    /// </summary>
    public static OcrResult Decode(ReadOnlySpan<float> plate, ReadOnlySpan<float> regions, OcrModelConfig config)
    {
        int classes = config.Alphabet.Length;
        if (plate.Length != config.Slots * classes)
            throw new InvalidDataException(
                $"The OCR model returned {plate.Length} plate values, the config needs {config.Slots} slots x {classes} characters.");

        var text = new System.Text.StringBuilder(config.Slots);
        var characters = new List<float[]>(config.Slots);
        bool padSeen = false, midPadding = false;

        for (int slot = 0; slot < config.Slots; slot++)
        {
            ReadOnlySpan<float> p = plate.Slice(slot * classes, classes);
            CheckProbabilities(p, "plate");

            int best = 0;
            for (int k = 1; k < classes; k++)
                if (p[k] > p[best]) best = k;

            if (best == config.PadIndex)
            {
                padSeen = true;
                continue;
            }

            if (padSeen)
                midPadding = true;

            text.Append(config.Alphabet[best]);
            characters.Add(p.ToArray());
        }

        float[]? regionProbabilities = null;
        if (config.Regions.Count > 0)
        {
            if (regions.Length != config.Regions.Count)
                throw new InvalidDataException(
                    $"The OCR model returned {regions.Length} region values, the config lists {config.Regions.Count} regions.");
            CheckProbabilities(regions, "region");
            regionProbabilities = regions.ToArray();
        }

        float confidence = characters.Count == 0 ? 0f : characters.Average(c => c.Max());
        return new OcrResult(text.ToString(), confidence, characters.ToArray(), regionProbabilities, midPadding);
    }

    /// <summary>The model must output probabilities (a softmax): each value 0..1, summing to 1.</summary>
    private static void CheckProbabilities(ReadOnlySpan<float> p, string output)
    {
        double sum = 0;
        foreach (float v in p)
        {
            if (!float.IsFinite(v) || v < -1e-4f || v > 1.0001f)
                throw new InvalidDataException($"The OCR model's '{output}' output is not a probability (value {v}).");
            sum += v;
        }

        if (Math.Abs(sum - 1) > 0.02)
            throw new InvalidDataException($"The OCR model's '{output}' output does not sum to 1 (sum {sum:0.###}): not a softmax.");
    }
}
