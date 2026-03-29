using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared;

namespace OpenClawTray.Services;

public enum GatewayProcessState
{
    Stopped,
    Starting,
    Running,
    AttachedExisting,
    Failed
}

/// <summary>
/// Manages the lifecycle of the local OpenClaw gateway process.
/// Attaches to an existing instance if one is running; otherwise spawns a new one.
/// Monitors for unexpected exits and restarts with a 1-second delay (KeepAlive).
/// </summary>
public sealed class GatewayProcessManager : IDisposable
{
    // Tunables
    private const int DefaultGatewayPort   = 18789;
    private const int LogLimitChars        = 20_000;
    private const int HealthProbeTimeoutMs = 2_000;
    private const int StartupPollIntervalMs = 400;
    private const int StartupTimeoutSeconds = 15;
    private const int AttachProbeTimeoutMs = 500;
    private const int AttachRetryIntervalMs = 250;
    private const int AttachMaxAttempts    = 3;
    private const int RestartDelayMs       = 1_000;

    private readonly IOpenClawLogger _logger;
    private readonly object _lock = new();

    private GatewayProcessState _state = GatewayProcessState.Stopped;
    private string _statusText = "Stopped";
    private string _log = string.Empty;
    private volatile bool _desiredActive;
    private Process? _gatewayProcess;
    private bool _disposed;

    public GatewayProcessState State { get { lock (_lock) return _state; } }
    public string StatusText { get { lock (_lock) return _statusText; } }
    public string Log { get { lock (_lock) return _log; } }
    public bool IsRunning => State is GatewayProcessState.Running or GatewayProcessState.AttachedExisting;

    public GatewayProcessManager(IOpenClawLogger logger)
    {
        _logger = logger;
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    public void Start()
    {
        _desiredActive = true;
        // Small delay so tray icon and settings initialize first.
        _ = Task.Run(async () =>
        {
            await Task.Delay(500).ConfigureAwait(false);
            if (_desiredActive) StartIfNeeded();
        });
    }

    public void Stop()
    {
        _desiredActive = false;
        KillGatewayProcess();
        SetStatus(GatewayProcessState.Stopped, "Stopped");
        _logger.Info("[GatewayProcessManager] Stopped");
    }

    public void RefreshLog()
    {
        var logPath = GatewayLogPath();
        _ = Task.Run(() =>
        {
            try
            {
                if (!File.Exists(logPath)) return;
                var text = ReadTail(logPath, LogLimitChars);
                lock (_lock) { _log = text; }
            }
            catch (Exception ex) { _logger.Debug($"[GatewayProcessManager] Failed to read gateway log: {ex.Message}"); }
        });
    }

    public async Task<bool> WaitForReadyAsync(TimeSpan timeout, CancellationToken ct)
    {
        var port     = GatewayPort();
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            if (!_desiredActive) return false;
            ct.ThrowIfCancellationRequested();

            if (await CanConnectToPortAsync(port, HealthProbeTimeoutMs).ConfigureAwait(false))
                return true;

            try { await Task.Delay(300, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }

        AppendLog("[gateway] readiness wait timed out\n");
        _logger.Warn("[GatewayProcessManager] Readiness wait timed out");
        return false;
    }

    // ─── Lifecycle internals ───────────────────────────────────────────────────

    private void StartIfNeeded()
    {
        lock (_lock)
        {
            if (_state is GatewayProcessState.Starting
                       or GatewayProcessState.Running
                       or GatewayProcessState.AttachedExisting)
                return;

            _state = GatewayProcessState.Starting;
            _statusText = "Starting…";
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (await AttachExistingIfAvailableAsync().ConfigureAwait(false)) return;
                await SpawnAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SetStatus(GatewayProcessState.Failed, ex.Message);
                _logger.Error("[GatewayProcessManager] Start failed unexpectedly", ex);
            }
        });
    }

    // ─── Attach existing ───────────────────────────────────────────────────────

    private async Task<bool> AttachExistingIfAvailableAsync()
    {
        var port        = GatewayPort();
        var hasListener = await CanConnectToPortAsync(port, AttachProbeTimeoutMs).ConfigureAwait(false);
        var maxAttempts = hasListener ? AttachMaxAttempts : 1;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (await CanConnectToPortAsync(port, HealthProbeTimeoutMs).ConfigureAwait(false))
            {
                var details = await GetPortDetailsAsync(port).ConfigureAwait(false) ?? $"port {port}";
                SetStatus(GatewayProcessState.AttachedExisting, $"Attached: {details}");
                AppendLog($"[gateway] using existing instance: {details}\n");
                _logger.Info($"[GatewayProcessManager] Attached to existing gateway: {details}");
                RefreshLog();

                _ = MonitorAttachedGatewayAsync(port);
                return true;
            }

            if (attempt < maxAttempts - 1)
                await Task.Delay(AttachRetryIntervalMs).ConfigureAwait(false);
        }

        if (hasListener)
        {
            // Something occupies the port but won't accept our probe — likely a non-gateway process.
            var reason = $"Port {port} is occupied but did not respond; check for port conflicts";
            SetStatus(GatewayProcessState.Failed, reason);
            AppendLog($"[gateway] attach failed: {reason}\n");
            _logger.Warn($"[GatewayProcessManager] Attach failed: {reason}");
            // Return true to prevent spawning a duplicate that would also fail.
            return true;
        }

        return false;
    }

    // Polls port every 3s while attached; triggers respawn if the gateway process exits.
    private async Task MonitorAttachedGatewayAsync(int port)
    {
        while (_desiredActive)
        {
            await Task.Delay(3_000).ConfigureAwait(false);
            if (!_desiredActive) return;
            if (State != GatewayProcessState.AttachedExisting) return;

            if (await CanConnectToPortAsync(port, HealthProbeTimeoutMs).ConfigureAwait(false)) continue;

            _logger.Warn($"[GatewayProcessManager] Attached gateway on port {port} stopped responding — restarting");
            AppendLog("[gateway] attached instance exited — restarting\n");

            lock (_lock) { _state = GatewayProcessState.Stopped; _statusText = "Stopped"; }
            if (_desiredActive) StartIfNeeded();
            return;
        }
    }

    // ─── Spawn ─────────────────────────────────────────────────────────────────

    private async Task SpawnAsync()
    {
        var port    = GatewayPort();
        var command = ResolveGatewayCommand(port);
        if (command is null)
        {
            const string reason = "openclaw not found in PATH — install via: npm install -g openclaw";
            SetStatus(GatewayProcessState.Failed, reason);
            AppendLog($"[gateway] {reason}\n");
            _logger.Error($"[GatewayProcessManager] {reason}");
            return;
        }

        AppendLog($"[gateway] spawning: {string.Join(" ", command)}\n");
        _logger.Info($"[GatewayProcessManager] Spawning: {string.Join(" ", command)}");

        try
        {
            KillGatewayProcess();

            var psi = new ProcessStartInfo
            {
                FileName               = command[0],
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            for (var i = 1; i < command.Length; i++)
                psi.ArgumentList.Add(command[i]);

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendLog(e.Data + "\n"); };
            proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) AppendLog(e.Data + "\n"); };
            proc.Exited             += OnGatewayExited;

            // Store reference before Start() so a very-fast exit in OnGatewayExited sees null cleanly.
            lock (_lock) { _gatewayProcess = proc; }

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            lock (_lock) { _gatewayProcess = null; }
            SetStatus(GatewayProcessState.Failed, ex.Message);
            AppendLog($"[gateway] spawn failed: {ex.Message}\n");
            _logger.Error("[GatewayProcessManager] Spawn failed", ex);
            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(StartupTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (!_desiredActive) return;

            if (await CanConnectToPortAsync(port, HealthProbeTimeoutMs).ConfigureAwait(false))
            {
                int? pid = null;
                lock (_lock) { try { pid = _gatewayProcess?.Id; } catch { } }
                var details = pid.HasValue ? $"pid {pid}" : "ok";
                SetStatus(GatewayProcessState.Running, $"Running ({details})");
                AppendLog($"[gateway] started: {details}\n");
                _logger.Info($"[GatewayProcessManager] Gateway started: {details}");
                RefreshLog();
                return;
            }

            await Task.Delay(StartupPollIntervalMs).ConfigureAwait(false);
        }

        // Timeout reached — do one final probe before marking as failed.
        // The gateway may have started slightly after the deadline.
        if (await CanConnectToPortAsync(port, HealthProbeTimeoutMs).ConfigureAwait(false))
        {
            int? pid = null;
            lock (_lock) { try { pid = _gatewayProcess?.Id; } catch { } }
            var details = pid.HasValue ? $"pid {pid}" : "ok";
            SetStatus(GatewayProcessState.Running, $"Running ({details})");
            AppendLog($"[gateway] started (late): {details}\n");
            _logger.Info($"[GatewayProcessManager] Gateway started (late): {details}");
            RefreshLog();
            return;
        }

        SetStatus(GatewayProcessState.Failed, "Gateway did not start in time");
        AppendLog("[gateway] start timed out\n");
        _logger.Warn("[GatewayProcessManager] Start timed out");
    }

    // ─── Process lifecycle ─────────────────────────────────────────────────────

    private void OnGatewayExited(object? sender, EventArgs e)
    {
        int? pid = null;
        try { pid = (sender as Process)?.Id; } catch { }
        AppendLog($"[gateway] process exited pid={pid}\n");
        _logger.Warn($"[GatewayProcessManager] Gateway process exited pid={pid}");

        lock (_lock) { _gatewayProcess = null; }
        if (!_desiredActive) return;

        // Gateway died unexpectedly — restart after a brief delay.
        // Reset to Stopped (not Starting) so StartIfNeeded() does not short-circuit.
        SetStatus(GatewayProcessState.Stopped, "Stopped (restarting…)");
        _ = Task.Run(async () =>
        {
            await Task.Delay(RestartDelayMs).ConfigureAwait(false);
            if (_desiredActive) StartIfNeeded();
        });
    }

    private void KillGatewayProcess()
    {
        Process? proc;
        lock (_lock)
        {
            proc = _gatewayProcess;
            _gatewayProcess = null;
        }
        if (proc is null) return;
        try
        {
            proc.Exited -= OnGatewayExited;
            if (!proc.HasExited)
                proc.Kill(entireProcessTree: true);
            proc.Dispose();
        }
        catch (Exception ex) { _logger.Debug($"[GatewayProcessManager] KillGatewayProcess non-fatal: {ex.Message}"); }
    }

    // ─── Gateway CLI resolution ────────────────────────────────────────────────

    private static int GatewayPort()
    {
        var env = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_PORT");
        if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out var p) && p > 0)
            return p;
        return DefaultGatewayPort;
    }

    private static string[]? ResolveGatewayCommand(int port)
    {
        var exe = FindOpenClawExecutable();
        if (exe is null) return null;
        return [exe, "gateway", "--port", $"{port}", "--bind", "loopback", "--allow-unconfigured"];
    }

    private static string? FindOpenClawExecutable()
    {
        var appData      = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        [
            Path.Combine(appData,      "npm", "openclaw.cmd"),
            Path.Combine(localAppData, "npm", "openclaw.cmd"),
        ];
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return FindInPath("openclaw") ?? FindInPath("openclaw.cmd");
    }

    private static string? FindInPath(string name)
    {
        try
        {
            var psi = new ProcessStartInfo("where.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            psi.ArgumentList.Add(name);

            using var p = Process.Start(psi);
            if (p is null) return null;

            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();

            var first = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim();
            return string.IsNullOrEmpty(first) ? null : first;
        }
        catch { return null; }
    }

    // ─── Network probes ────────────────────────────────────────────────────────

    private static async Task<bool> CanConnectToPortAsync(int port, int timeoutMs)
    {
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await tcp.ConnectAsync(IPAddress.Loopback, port, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch { return false; }
    }

    private static async Task<string?> GetPortDetailsAsync(int port)
    {
        try
        {
            var psi = new ProcessStartInfo("netstat.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            psi.ArgumentList.Add("-ano");
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add("TCP");

            using var p = Process.Start(psi);
            if (p is null) return null;

            var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await p.WaitForExitAsync().ConfigureAwait(false);

            var portSuffix = $":{port}";
            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5
                    && parts[0].Equals("TCP",       StringComparison.OrdinalIgnoreCase)
                    && parts[1].EndsWith(portSuffix, StringComparison.Ordinal)
                    && parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
                    return $"pid {parts[4]}, port {port}";
            }
        }
        catch { }
        return null;
    }

    // ─── Log file ──────────────────────────────────────────────────────────────

    private static string GatewayLogPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "OpenClaw", "logs", "gateway.log");
    }

    private static string ReadTail(string path, int limit)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length <= limit)
        {
            using var r = new StreamReader(fs);
            return r.ReadToEnd();
        }
        fs.Seek(-limit, SeekOrigin.End);
        using var tailReader = new StreamReader(fs);
        return tailReader.ReadToEnd();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private void SetStatus(GatewayProcessState state, string text)
    {
        lock (_lock) { _state = state; _statusText = text; }
    }

    private void AppendLog(string chunk)
    {
        lock (_lock)
        {
            _log += chunk;
            // Ring buffer — keep the most recent LogLimitChars characters.
            if (_log.Length > LogLimitChars)
                _log = _log[(_log.Length - LogLimitChars)..];
        }
    }

    // ─── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
