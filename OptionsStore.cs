using System.Text.Json;

namespace MockRtsp;

/// <summary>
/// Keeps the stream options between runs, in %APPDATA%\MockRtsp\options.json.
/// Note that the viewer and publisher passwords are stored there as plain text.
/// </summary>
public static class OptionsStore
{
    private static readonly JsonSerializerOptions JsonSettings = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockRtsp", "options.json");

    /// <summary>Returns the saved options, or the defaults if none are saved or the file is unusable. Never throws.</summary>
    public static StreamOptions Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new StreamOptions();

            var loaded = JsonSerializer.Deserialize<StreamOptions>(File.ReadAllText(FilePath), JsonSettings);

            return loaded is not null && loaded.Validate().Count == 0
                ? loaded
                : new StreamOptions();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Reading saved stream options");
            return new StreamOptions();
        }
    }

    /// <summary>Saves the options. Returns false if the file could not be written. Never throws.</summary>
    public static bool Save(StreamOptions options)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(options, JsonSettings));
            return true;
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Saving stream options");
            return false;
        }
    }
}
