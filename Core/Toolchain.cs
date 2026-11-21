namespace MockRtsp;

/// <summary>
/// Finds the bundled MediaMTX and FFmpeg executables. They live in folders next to
/// MockRtsp.exe:
///   mediamtx\mediamtx.exe
///   ffmpeg\ffmpeg.exe   (or ffmpeg\bin\ffmpeg.exe for archives that unpack a bin folder)
/// </summary>
public static class Toolchain
{
    public const string MediaMtxFolder = "mediamtx";
    public const string FfmpegFolder = "ffmpeg";

    /// <summary>Returns the full path to the executable, or null if it is not found. Never throws.</summary>
    public static string? Find(string folder, string exeName)
    {
        try
        {
            string root = Path.Combine(AppContext.BaseDirectory, folder);

            if (!Directory.Exists(root))
                return null;

            string direct = Path.Combine(root, exeName);
            if (File.Exists(direct))
                return direct;

            return Directory.EnumerateFiles(root, exeName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, $"Searching for {exeName} in {folder}");
            return null;
        }
    }
}
