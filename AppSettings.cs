namespace MockRtsp;

/// <summary>
/// Remembers the MediaMTX and FFmpeg executable paths between runs.
/// Stored in %APPDATA%\MockRtsp\settings.ini as:
///   [Paths]
///   MediaMtx=C:\tools\mediamtx.exe
///   FFmpeg=C:\ffmpeg\bin\ffmpeg.exe
/// </summary>
public sealed class AppSettings
{
    private const string Section = "[Paths]";

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MockRtsp",
        "settings.ini");

    /// <summary>Empty means use the embedded copy of MediaMTX.</summary>
    public string MediaMtxPath { get; set; } = string.Empty;

    /// <summary>Empty means look up "ffmpeg" on PATH.</summary>
    public string FfmpegPath { get; set; } = string.Empty;

    /// <summary>Loads settings. Never throws; a damaged file is logged and defaults are used.</summary>
    public static AppSettings Load()
    {
        var settings = new AppSettings();

        try
        {
            if (!File.Exists(FilePath))
                return settings;

            foreach (var raw in File.ReadAllLines(FilePath))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line[0] is ';' or '#' or '[')
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string key = line[..eq].Trim();
                string value = line[(eq + 1)..].Trim();

                switch (key.ToLowerInvariant())
                {
                    case "mediamtx":
                        settings.MediaMtxPath = value;
                        break;
                    case "ffmpeg":
                        settings.FfmpegPath = value;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, $"Reading settings from {FilePath}");
        }

        return settings;
    }

    /// <summary>Writes the settings file. Throws on failure so the caller can tell the user.</summary>
    public void Save()
    {
        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string text =
            Section + Environment.NewLine +
            "MediaMtx=" + MediaMtxPath + Environment.NewLine +
            "FFmpeg=" + FfmpegPath + Environment.NewLine;

        // Write to a temp file and then move it, so a crash never leaves a half-written file.
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, FilePath, overwrite: true);
    }
}
