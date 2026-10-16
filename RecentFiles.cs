namespace MockRtsp;

/// <summary>
/// Remembers the video files most recently added as cameras.
/// Stored in %APPDATA%\MockRtsp\recent-files.txt, one full path per line, newest first.
/// </summary>
public static class RecentFiles
{
    private const int MaxItems = 10;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockRtsp", "recent-files.txt");

    /// <summary>Returns the remembered files that still exist, newest first. Never throws.</summary>
    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new List<string>();

            return File.ReadAllLines(FilePath)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && File.Exists(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxItems)
                .ToList();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Reading recent files");
            return new List<string>();
        }
    }

    /// <summary>Moves the file to the top of the list and saves it. Never throws.</summary>
    public static void Add(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);

            var list = Load();
            list.RemoveAll(p => p.Equals(full, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, full);

            if (list.Count > MaxItems)
                list.RemoveRange(MaxItems, list.Count - MaxItems);

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, list);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Saving recent files");
        }
    }
}
