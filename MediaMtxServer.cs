using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;

namespace MockRtsp;

public sealed class MediaMtxServer : IDisposable
{
    private readonly Process _process;
    private readonly string _workDir;

    public int Port { get; }

    private MediaMtxServer(Process process, string workDir, int port)
    {
        _process = process;
        _workDir = workDir;
        Port = port;
    }

    public string BuildUrl(string path) => $"rtsp://127.0.0.1:{Port}/{path.TrimStart('/')}";

    public static async Task<MediaMtxServer> StartAsync(
        int port = 8554,
        string? mediaMtxPath = null,
        TimeSpan? readyTimeout = null,
        CancellationToken ct = default)
    {
        string exe = mediaMtxPath ?? ExtractEmbeddedBinary();

        string workDir = Path.Combine(Path.GetTempPath(), "mock-rtsp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

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

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start MediaMTX.");

        // Drain output so the server never blocks on a full pipe.
        process.OutputDataReceived += (_, e) => Debug.WriteLine(e.Data);
        process.ErrorDataReceived += (_, e) => Debug.WriteLine(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var server = new MediaMtxServer(process, workDir, port);
        try
        {
            await WaitForPortAsync(process, port, readyTimeout ?? TimeSpan.FromSeconds(10), ct);
        }
        catch
        {
            server.Dispose();
            throw;
        }

        return server;
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

    private static async Task WaitForPortAsync(
        Process process, int port, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (process.HasExited)
                throw new InvalidOperationException(
                    $"MediaMTX exited with code {process.ExitCode}. Port {port} may already be in use.");

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", port, ct);
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
                $"Embedded resource '{resourceName}' not found. Pass mediaMtxPath explicitly.");

        // Write to a unique temp file first, then move, so parallel test runs don't collide.
        string tmp = target + "." + Guid.NewGuid().ToString("N");
        using (var file = File.Create(tmp))
            stream.CopyTo(file);

        File.Move(tmp, target, overwrite: true);
        return target;
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        finally
        {
            _process.Dispose();
        }

        try
        {
            Directory.Delete(_workDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Temp cleanup is best-effort.
        }
    }
}
