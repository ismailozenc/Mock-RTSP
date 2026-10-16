using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

    public string BuildUrl(string path) => BuildUrl(path, null, null);

    /// <summary>Builds an rtsp:// URL. Login details go in the user part when a user name is given.</summary>
    public string BuildUrl(string path, string? user, string? password)
    {
        string login = string.IsNullOrEmpty(user)
            ? string.Empty
            : $"{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(password ?? string.Empty)}@";

        return $"rtsp://{login}127.0.0.1:{Port}/{path.TrimStart('/')}";
    }

    /// <summary>
    /// True when MediaMTX is serving the path, checked the way a viewer would: an RTSP DESCRIBE
    /// that must answer 200 OK. Never throws.
    /// </summary>
    public async Task<bool> IsPathReadyAsync(string path, string? user, string? password, CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, Port, timeout.Token);
            var stream = client.GetStream();

            var request = new StringBuilder()
                .Append("DESCRIBE ").Append(BuildUrl(path)).Append(" RTSP/1.0\r\n")
                .Append("CSeq: 1\r\n")
                .Append("Accept: application/sdp\r\n");

            if (!string.IsNullOrEmpty(user))
            {
                string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
                request.Append("Authorization: Basic ").Append(token).Append("\r\n");
            }

            request.Append("\r\n");
            await stream.WriteAsync(Encoding.ASCII.GetBytes(request.ToString()), timeout.Token);

            var buffer = new byte[64];
            int read = await stream.ReadAsync(buffer, timeout.Token);
            return Encoding.ASCII.GetString(buffer, 0, read).StartsWith("RTSP/1.0 200", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <param name="mediaMtxPath">Full path to mediamtx.exe.</param>
    /// <param name="log">Optional callback for status messages.</param>
    /// <param name="options">Server settings. Null uses the defaults.</param>
    public static async Task<MediaMtxServer> StartAsync(
        int port,
        string mediaMtxPath,
        TimeSpan? readyTimeout = null,
        CancellationToken ct = default,
        Action<string>? log = null,
        StreamOptions? options = null)
    {
        // RTP and RTCP use port+1000 and port+1001, so the port must leave room for them.
        if (port < 1025 || port > 65535 - 1001)
            throw new ArgumentOutOfRangeException(nameof(port),
                "Port must be between 1025 and 64534 (RTP and RTCP use port + 1000 and port + 1001).");

        var settings = options ?? new StreamOptions();
        var problems = settings.Validate();
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(options));

        string exe = mediaMtxPath;

        // Leftovers from an earlier run would hold the ports, so end them before checking.
        int stopped = StopStaleInstances(exe);
        if (stopped > 0)
            log?.Invoke($"Stopped {stopped} leftover MediaMTX process(es) from an earlier run.");

        EnsurePortFree(port);

        string workDir = Path.Combine(Path.GetTempPath(), "mock-rtsp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        Process? process = null;
        MediaMtxServer? server = null;
        var output = new Queue<string>();

        try
        {
            string configPath = Path.Combine(workDir, "mediamtx.yml");
            await File.WriteAllTextAsync(configPath, BuildConfig(port, settings), ct);

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

            // Drain output so the server never blocks on a full pipe. Keep the last lines,
            // so a failed start can say why (for example a rejected config value).
            process.OutputDataReceived += (_, e) => Remember(output, e.Data);
            process.ErrorDataReceived += (_, e) => Remember(output, e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            server = new MediaMtxServer(process, workDir, port);
            await WaitForPortAsync(process, port, readyTimeout ?? TimeSpan.FromSeconds(10), ct);
            return server;
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException && process is not null && HasExitedQuietly(process))
        {
            // MediaMTX quit by itself. Its last output says why, so pass that on.
            process.WaitForExit();                  // lets the remaining output arrive
            string details;
            lock (output)
            {
                details = string.Join(Environment.NewLine, output);
            }

            server?.Dispose();
            if (server is null)
            {
                KillQuietly(process);
                DeleteDirQuietly(workDir);
            }

            throw new InvalidOperationException(
                details.Length > 0 ? $"MediaMTX could not start:{Environment.NewLine}{details}" : ex.Message, ex);
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
                    $"Could not run MediaMTX at '{exe}'. Check that the file is a valid Windows executable.", win32);

            throw;
        }
    }

    private static string BuildConfig(int port, StreamOptions options)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"logLevel: {options.MediaMtxLogLevel}");
        sb.AppendLine("rtsp: yes");
        sb.AppendLine($"rtspAddress: :{port}");
        sb.AppendLine($"rtspTransports: [{TransportList(options.Transports)}]");
        sb.AppendLine($"readTimeout: {options.ReadTimeoutSeconds}s");
        sb.AppendLine($"writeTimeout: {options.WriteTimeoutSeconds}s");
        sb.AppendLine($"rtpAddress: :{port + 1000}");
        sb.AppendLine($"rtcpAddress: :{port + 1001}");
        sb.AppendLine("rtmp: no");
        sb.AppendLine("hls: no");
        sb.AppendLine("webrtc: no");
        sb.AppendLine("srt: no");
        sb.AppendLine("api: no");
        sb.AppendLine("metrics: no");
        // Logins. "any" with an empty password means no login is needed for that action.
        sb.AppendLine("authInternalUsers:");
        AppendUser(sb, options.PublisherUser, options.PublisherPassword, "publish");
        AppendUser(sb, options.ViewerUser, options.ViewerPassword, "read", "playback");

        sb.AppendLine("paths:");
        sb.AppendLine("  all_others:");

        return sb.ToString();
    }

    private static void AppendUser(StringBuilder sb, string user, string password, params string[] actions)
    {
        bool open = user.Length == 0;

        sb.AppendLine($"  - user: {(open ? "any" : Quote(user))}");
        sb.AppendLine($"    pass: {(open ? "\"\"" : Quote(password))}");
        sb.AppendLine("    ips: []");
        sb.AppendLine("    permissions:");
        foreach (string action in actions)
            sb.AppendLine($"      - action: {action}");
    }

    private static void Remember(Queue<string> output, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (output)
        {
            output.Enqueue(line.Trim());
            while (output.Count > 10)
                output.Dequeue();
        }
    }

    private static bool HasExitedQuietly(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private static string TransportList(RtspTransportMode mode) => mode switch
    {
        RtspTransportMode.TcpOnly => "tcp",
        RtspTransportMode.UdpOnly => "udp",
        _ => "tcp, udp"
    };

    /// <summary>Quotes a YAML string, escaping backslashes and double quotes.</summary>
    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>
    /// Ends mediamtx processes started from the same executable path as the one about to run.
    /// Other MediaMTX instances (different path, or started by another user) are left alone.
    /// </summary>
    private static int StopStaleInstances(string exe)
    {
        string target = Path.GetFullPath(exe);
        int stopped = 0;

        foreach (var process in Process.GetProcessesByName("mediamtx"))
        {
            using (process)
            {
                try
                {
                    string? path = process.MainModule?.FileName;
                    if (path is null || !string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
                        continue;

                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                    stopped++;
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // Already exited, or not ours to inspect. Either way, skip it.
                }
            }
        }

        return stopped;
    }

    private static void EnsurePortFree(int port)
    {
        // MediaMTX binds TCP for RTSP and UDP for RTP and RTCP, so all three must be free.
        CheckPort(port, ProtocolType.Tcp, "RTSP");
        CheckPort(port + 1000, ProtocolType.Udp, "RTP");
        CheckPort(port + 1001, ProtocolType.Udp, "RTCP");
    }

    private static void CheckPort(int port, ProtocolType protocol, string purpose)
    {
        try
        {
            if (protocol == ProtocolType.Tcp)
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                listener.Stop();
            }
            else
            {
                using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            }
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"The {purpose} port {port} is already in use by another program. This is often a " +
                "MediaMTX process left running from an earlier session. End mediamtx.exe in Task Manager " +
                "or choose another port.", ex);
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
