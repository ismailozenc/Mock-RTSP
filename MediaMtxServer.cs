using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace MockRtsp;

public sealed class MediaMtxServer : IDisposable
{
    private readonly Process _process;
    private readonly string _workDir;
    private bool _disposed;

    public int Port { get; }

    private MediaMtxServer(Process process, string workDir, int port)
    {
        _process = process;
        _workDir = workDir;
        Port = port;
    }

    public bool IsRunning
    {
        get
        {
            if (_disposed) return false;
            try
            {
                return !_process.HasExited;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                return false;
            }
        }
    }

    public string BuildUrl(string path) => $"rtsp://127.0.0.1:{Port}/{path.TrimStart('/')}";

    public static async Task<MediaMtxServer> StartAsync(
        int port = 8554,
        string? mediaMtxPath = null,
        TimeSpan? readyTimeout = null,
        CancellationToken ct = default)
    {
        // RTP and RTCP use port+1000 and port+1001, so the port must leave room for them.
        if (port < 1025 || port > 65535 - 1001)
            throw new ArgumentOutOfRangeException(nameof(port),
                "Port must be between 1025 and 64534 (RTP and RTCP use port + 1000 and port + 1001).");

        EnsurePortFree(port);

        string exe = mediaMtxPath ?? ExtractEmbeddedBinary();
        string workDir = Path.Combine(Path.GetTempPath(), "mock-rtsp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        Process? process = null;
        MediaMtxServer? server = null;

        try
        {
            string configPath = Path.Combine(workDir, "mediamtx.yml");
            await File.WriteAllTextAsync(configPath, BuildConfig(port), ct);

            var psi = new ProcessStartInfo(exe, $"\"{configPath}\"")
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start MediaMTX.");

            // Drain output so the server never blocks on a full pipe.
            process.OutputDataReceived += (_, e) => Debug.WriteLine(e.Data);
            process.ErrorDataReceived += (_, e) => Debug.WriteLine(e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            server = new MediaMtxServer(process, workDir, port);
            await WaitForPortAsync(process, port, readyTimeout ?? TimeSpan.FromSeconds(10), ct);
            return server;
        }
        catch (Exception ex)
        {
            if (server is not null)
            {
                server.Dispose();                   // also kills the process and removes workDir
            }
            else
            {
                KillQuietly(process);
                DeleteDirQuietly(workDir);
            }

            if (ex is Win32Exception win32)
                throw new InvalidOperationException(
                    $"Could not run MediaMTX at '{exe}'. Check the path and that it is a Windows executable.", win32);

            throw;
        }
    }

    private static string BuildConfig(int port) => $"""
        logLevel: warn
        rtsp: yes
        rtspAddress: :{port}
        rtspTransports: [tcp, udp]
        rtpAddress: :{port + 1000}
        rtcpAddress: :{port + 1001}
        rtmp: no
        hls: no
        webrtc: no
        srt: no
        api: no
        metrics: no
        paths:
          all_others:
        """;

    private static void EnsurePortFree(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException($"Port {port} is already in use. Choose another port.", ex);
        }
        finally
        {
            try
            {
                listener?.Stop();
            }
            catch (Exception ex)
            {
                ErrorReporter.Report(ex, "Releasing port probe");
            }
        }
    }

    private static async Task WaitForPortAsync(
        Process process, int port, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (process.HasExited)
                throw new InvalidOperationException(
                    $"MediaMTX exited with code {process.ExitCode}. Check the config and the log output.");

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, ct);
                return;
            }
            catch (SocketException)
            {
                // Not listening yet.
            }

            await Task.Delay(100, ct);
        }

        throw new TimeoutException($"MediaMTX did not open port {port} within {timeout}.");
    }

    private static string ExtractEmbeddedBinary()
    {
        const string resourceName = "MockRtsp.Binaries.mediamtx.exe";
        string target = Path.Combine(Path.GetTempPath(), "mock-rtsp-mediamtx.exe");

        if (File.Exists(target))
            return target;

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                "MediaMTX is not embedded in this build. Choose mediamtx.exe in the MediaMTX field.");

        // Write to a unique temp file first, then move, so parallel runs don't collide.
        string tmp = target + "." + Guid.NewGuid().ToString("N");
        using (var file = File.Create(tmp))
            stream.CopyTo(file);

        File.Move(tmp, target, overwrite: true);
        return target;
    }

    private static void KillQuietly(Process? process)
    {
        if (process is null) return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, "Stopping MediaMTX");
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void DeleteDirQuietly(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, $"Removing temp folder {dir}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        KillQuietly(_process);
        DeleteDirQuietly(_workDir);
    }
}
