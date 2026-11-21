using System.Text.Json;

namespace MockRtsp;

/// <summary>What the scanner remembers between runs, in %APPDATA%\MockRtsp\scanner.json.</summary>
internal sealed class ScannerOptions
{
    public const int MaxRecentFolders = 10;

    public string Folder { get; set; } = string.Empty;

    /// <summary>"en" or "tr"; empty until chosen (the first run follows Windows).</summary>
    public string Language { get; set; } = string.Empty;
    public List<string> RecentFolders { get; set; } = new();

    // Gap settings for a video scanned for the first time (each video then keeps its own).
    public double GapThresholdPercent { get; set; } = 0.3;
    public double MinGapSeconds { get; set; } = 3;
    public double KeepAroundSeconds { get; set; } = 1;

    // The queue: measure / read again even when done before.
    public bool GapsAgain { get; set; }
    public bool PlatesAgain { get; set; }

    public MotionSettings GapDefaults => new(GapThresholdPercent, MinGapSeconds, KeepAroundSeconds);

    private static readonly JsonSerializerOptions JsonSettings = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockRtsp", "scanner.json");

    /// <summary>The saved options, or the defaults when none are saved or the file is unusable (reported). Never throws.</summary>
    public static ScannerOptions Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new ScannerOptions();

            var loaded = JsonSerializer.Deserialize<ScannerOptions>(File.ReadAllText(FilePath), JsonSettings);
            if (loaded is not null && loaded.GapDefaults.Validate().Count == 0)
                return loaded;

            ErrorReporter.Report(new InvalidDataException("scanner.json has values out of range"), "Reading scanner options");
            return new ScannerOptions();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Reading scanner options");
            return new ScannerOptions();
        }
    }

    /// <summary>Saves the options. False when the file could not be written. Never throws.</summary>
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonSettings));
            return true;
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Saving scanner options");
            return false;
        }
    }

    public void Remember(string folder)
    {
        Folder = folder;
        RecentFolders.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
        RecentFolders.Insert(0, folder);
        if (RecentFolders.Count > MaxRecentFolders)
            RecentFolders.RemoveRange(MaxRecentFolders, RecentFolders.Count - MaxRecentFolders);
    }
}
