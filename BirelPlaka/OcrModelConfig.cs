using System.Globalization;

namespace Birel_Plaka;

/// <summary>
/// The plate config that belongs to the OCR model (fast-plate-ocr's "*_plate_config.yaml", shipped
/// next to the model and selected with [Detection] OcrConfig): character alphabet, end-of-plate
/// (pad) character, number of character slots, input image size and the country labels of the
/// model's "region" output. Read once at start-up; <see cref="PlateOCR"/> checks the model's inputs
/// and outputs against it, so a model and a config that do not belong together stop the program.
///
/// Only the flat subset of YAML these files use is read: "key: value" lines, quoted strings, and a
/// [ ... ] list that may span several lines. Comments (#) and unknown keys are ignored.
/// </summary>
public sealed class OcrModelConfig
{
    private OcrModelConfig(string alphabet, char padChar, int slots, int imageWidth, int imageHeight, IReadOnlyList<string> regions)
    {
        Alphabet = alphabet;
        PadChar = padChar;
        PadIndex = alphabet.IndexOf(padChar);
        Slots = slots;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        Regions = regions;
    }

    /// <summary>Characters of the model's per-slot softmax, in output order (pad character included).</summary>
    public string Alphabet { get; }

    /// <summary>Marks "no character in this slot" (end of plate).</summary>
    public char PadChar { get; }
    public int PadIndex { get; }

    /// <summary>Character slots per plate (the model's "plate" output is [N, Slots, Alphabet.Length]).</summary>
    public int Slots { get; }

    public int ImageWidth { get; }
    public int ImageHeight { get; }

    /// <summary>Labels of the "region" (country) output, in output order. Empty: the model has none.</summary>
    public IReadOnlyList<string> Regions { get; }

    public static OcrModelConfig Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>Reads the config; any missing, unsupported or inconsistent value throws <see cref="InvalidDataException"/>.</summary>
    public static OcrModelConfig Parse(string text)
    {
        var values = ReadFlatYaml(text);

        string Required(string key) =>
            values.TryGetValue(key, out var v) && v.Length > 0
                ? v
                : throw new InvalidDataException($"'{key}' is missing.");

        int Int(string key, int min, int max)
        {
            string raw = Required(key);
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < min || value > max)
                throw new InvalidDataException($"'{key}: {raw}' must be a whole number between {min} and {max}.");
            return value;
        }

        string alphabet = Unquote(Required("alphabet"));
        if (alphabet.Length < 2 || alphabet.Distinct().Count() != alphabet.Length)
            throw new InvalidDataException("'alphabet' must have at least two characters, none repeated.");

        string pad = Unquote(Required("pad_char"));
        if (pad.Length != 1 || !alphabet.Contains(pad[0]))
            throw new InvalidDataException("'pad_char' must be one character of the alphabet.");

        int slots = Int("max_plate_slots", 1, 32);
        int height = Int("img_height", 8, 1024);
        int width = Int("img_width", 8, 1024);

        // What PlateOCR feeds the model: the crop stretched to width x height, RGB, bytes.
        if (values.TryGetValue("keep_aspect_ratio", out var keep) && !keep.Equals("false", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"'keep_aspect_ratio: {keep}' is not supported (only false).");
        if (values.TryGetValue("image_color_mode", out var mode) && !Unquote(mode).Equals("rgb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"'image_color_mode: {mode}' is not supported (only rgb).");
        if (values.TryGetValue("interpolation", out var interpolation) && !Unquote(interpolation).Equals("linear", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"'interpolation: {interpolation}' is not supported (only linear).");

        var regions = new List<string>();
        if (values.TryGetValue("plate_regions", out var list))
        {
            string inner = list.Trim();
            if (!inner.StartsWith('[') || !inner.EndsWith(']'))
                throw new InvalidDataException("'plate_regions' must be a [ ... ] list.");

            foreach (string item in SplitList(inner[1..^1]))
                regions.Add(Unquote(item));

            if (regions.Any(r => r.Length == 0) || regions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != regions.Count)
                throw new InvalidDataException("'plate_regions' must not contain empty or repeated names.");
        }

        return new OcrModelConfig(alphabet, pad[0], slots, width, height, regions);
    }

    /// <summary>Index of a region label (case-insensitive), or -1.</summary>
    public int RegionIndex(string name)
    {
        for (int i = 0; i < Regions.Count; i++)
            if (string.Equals(Regions[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static Dictionary<string, string> ReadFlatYaml(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? openKey = null;
        var open = new System.Text.StringBuilder();

        foreach (string rawLine in text.Replace("\r", "").Split('\n'))
        {
            string line = StripComment(rawLine);

            if (openKey != null)
            {
                open.Append(' ').Append(line.Trim());
                if (line.Contains(']'))
                {
                    values[openKey] = open.ToString().Trim();
                    openKey = null;
                }
                continue;
            }

            if (line.Trim().Length == 0)
                continue;

            int colon = line.IndexOf(':');
            if (colon <= 0 || char.IsWhiteSpace(line[0]))
                throw new InvalidDataException($"Unexpected line: {rawLine.Trim()}");

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();

            if (value.StartsWith('[') && !value.Contains(']'))
            {
                openKey = key;
                open.Clear().Append(value);
                continue;
            }

            if (!values.TryAdd(key, value))
                throw new InvalidDataException($"'{key}' appears twice.");
        }

        if (openKey != null)
            throw new InvalidDataException($"The list '{openKey}' is not closed with ']'.");

        return values;
    }

    /// <summary>The line without a # comment (a # inside quotes is kept).</summary>
    private static string StripComment(string line)
    {
        char quote = '\0';
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
            }
            else if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '#')
            {
                return line[..i];
            }
        }
        return line;
    }

    private static IEnumerable<string> SplitList(string inner)
    {
        var item = new System.Text.StringBuilder();
        char quote = '\0';

        foreach (char c in inner)
        {
            if (quote != '\0')
            {
                item.Append(c);
                if (c == quote) quote = '\0';
            }
            else if (c is '\'' or '"')
            {
                quote = c;
                item.Append(c);
            }
            else if (c == ',')
            {
                yield return item.ToString().Trim();
                item.Clear();
            }
            else
            {
                item.Append(c);
            }
        }

        if (quote != '\0')
            throw new InvalidDataException("A quoted list item is not closed.");

        if (item.ToString().Trim().Length > 0)
            yield return item.ToString().Trim();
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && (value[0] is '\'' or '"') && value[^1] == value[0])
            return value[1..^1];
        return value;
    }
}
