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

    /// <summary>
    /// Opens the URL in a new VLC window and returns its process, so the caller can close it.
    /// Low network caching makes the picture appear sooner. RTSP over TCP avoids lost UDP packets.
    /// </summary>
    public static Process Open(string vlcPath, string url, string title)
    {
        var psi = new ProcessStartInfo(vlcPath)
        {
            UseShellExecute = false
        };
        psi.ArgumentList.Add("--no-one-instance");      // always a separate window we can close later
        psi.ArgumentList.Add("--network-caching=300");
        psi.ArgumentList.Add("--rtsp-tcp");
        psi.ArgumentList.Add($"--meta-title={title}");
        psi.ArgumentList.Add(url);

        try
        {
            return Process.Start(psi) ?? throw new InvalidOperationException("VLC did not start.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Could not run VLC at '{vlcPath}'.", ex);
        }
    }

    /// <summary>Closes a VLC window opened by Open. Never throws.</summary>
    public static void Close(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Closing VLC");
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>True if the VLC window is still open. Never throws.</summary>
    public static bool IsOpen(Process? process)
    {
        try
        {
            return process is not null && !process.HasExited;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }
}
