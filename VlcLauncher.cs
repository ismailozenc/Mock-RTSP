using System.ComponentModel;
using System.Diagnostics;

namespace MockRtsp;

/// <summary>Finds VLC and opens a stream in it.</summary>
public static class VlcLauncher
{
    /// <summary>
    /// Looks for vlc.exe in a bundled "vlc" folder next to MockRtsp.exe, then in the
    /// usual install folders. Returns null when VLC is not found. Never throws.
    /// </summary>
    public static string? Find()
    {
        string? bundled = Toolchain.Find("vlc", "vlc.exe");
        if (bundled is not null)
            return bundled;

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root))
                continue;

            string candidate = Path.Combine(root, "VideoLAN", "VLC", "vlc.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>Opens the URL in VLC. Low network caching makes the picture appear sooner.</summary>
    public static void Open(string vlcPath, string url)
    {
        var psi = new ProcessStartInfo(vlcPath)
        {
            UseShellExecute = false
        };
        psi.ArgumentList.Add("--network-caching=300");
        psi.ArgumentList.Add(url);

        try
        {
            using Process? process = Process.Start(psi);
            if (process is null)
                throw new InvalidOperationException("VLC did not start.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Could not run VLC at '{vlcPath}'.", ex);
        }
    }
}
