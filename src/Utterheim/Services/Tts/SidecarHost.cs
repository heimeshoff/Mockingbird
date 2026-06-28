using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Utterheim.Services.Settings;

namespace Utterheim.Services.Tts;

/// <summary>
/// Owns the lifecycle of the pocket-tts Python sidecar process per ADR 0002:
///
///  - Spawns <c>python.exe -m pocket_tts serve --host 127.0.0.1 --port N</c>.
///  - Polls <c>GET /health</c> until ready (the engine awaits <see cref="EnsureReadyAsync"/> before synthesis).
///  - Captures stdout/stderr line-by-line into Serilog with a "sidecar" source enrichment.
///  - Tears down with graceful kill-on-timeout when the host stops.
///  - Restarts on crash with capped exponential backoff; gives up after a few attempts.
///
/// Bound to loopback only (vision: no network exposure of TTS).
/// </summary>
public sealed class SidecarHost : IHostedService, IDisposable
{
    private static readonly Regex PortRegex = new(@"Uvicorn running on https?://[^:]+:(\d+)",
        RegexOptions.Compiled);

    private readonly DataPathService _paths;
    private readonly PythonRuntimeBootstrapper _bootstrapper;
    private readonly ILogger<SidecarHost> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly object _stateLock = new();
    private SidecarState _state = SidecarState.NotStarted;
    private bool _lastHealthCheckSucceeded;
    private string? _lastError;
    private int _port;

    private Process? _process;
    private CancellationTokenSource? _supervisorCts;
    private Task? _supervisorTask;
    private TaskCompletionSource<bool>? _readyTcs;

    // Win32 Job Object that owns every python.exe (and descendant) we spawn.
    // KILL_ON_JOB_CLOSE means: on Dispose, *and* on host-process death, Windows
    // atomically kills every member of the job. This is the load-bearing
    // anti-zombie mechanism per main-022; tree-walking Process.Kill alone has
    // races when grandchildren get re-parented or when the host is killed
    // outright.
    private readonly ProcessJobObject _jobObject = new();

    // Set true during StopAsync so the supervisor's restart loop bails out
    // even if it sees the sidecar process exit before noticing ct cancellation.
    // Belt-and-suspenders against the auto-restart-during-shutdown race noted
    // in main-018 criterion 4 / main-022.
    private volatile bool _shuttingDown;

    public SidecarHost(
        DataPathService paths,
        PythonRuntimeBootstrapper bootstrapper,
        ILogger<SidecarHost> logger,
        ILoggerFactory loggerFactory)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>HTTP base URL of the running sidecar (e.g. <c>http://127.0.0.1:54123</c>). Throws if not started.</summary>
    public string BaseUrl
    {
        get
        {
            lock (_stateLock)
            {
                if (_port == 0)
                    throw new InvalidOperationException("Sidecar is not yet listening; await EnsureReadyAsync first.");
                return $"http://127.0.0.1:{_port}";
            }
        }
    }

    /// <summary>Snapshot of sidecar status for the <c>/status</c> endpoint.</summary>
    public SidecarStatus GetStatus()
    {
        lock (_stateLock)
        {
            return new SidecarStatus(_state, _lastHealthCheckSucceeded, _port, _lastError);
        }
    }

    /// <summary>
    /// Raised on the thread that mutates state (typically the supervisor task).
    /// Subscribers must marshal to the UI dispatcher themselves. The status
    /// footer view-model uses this to keep "Engine: {state}" live.
    /// </summary>
    public event EventHandler<SidecarStatus>? StateChanged;

    /// <summary>
    /// Wait until the sidecar reports healthy. Used by the engine before issuing
    /// the first synthesis request. Times out via <paramref name="ct"/>.
    /// </summary>
    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_stateLock)
        {
            if (_state == SidecarState.Running && _lastHealthCheckSucceeded) return;
            if (_state == SidecarState.Failed)
                throw new InvalidOperationException(_lastError ?? "Sidecar is in a failed state.");
            tcs = _readyTcs;
        }

        if (tcs is null)
            throw new InvalidOperationException("Sidecar is not started.");

        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
        await tcs.Task.ConfigureAwait(false);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_supervisorTask is not null)
                return Task.CompletedTask;
            _supervisorCts = new CancellationTokenSource();
            _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _supervisorTask = Task.Run(() => SuperviseAsync(_supervisorCts!.Token));
        _logger.LogInformation("SidecarHost supervisor started.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// User-initiated restart cycle (main-017): cancel the current supervisor,
    /// terminate the running python process, reset internal state, and start
    /// the supervisor again. Distinct from <see cref="StopAsync"/> — this path
    /// does <em>not</em> dispose the JobObject (the host is still alive and
    /// will spawn a new sidecar into it).
    ///
    /// The transition is announced as <see cref="SidecarState.Restarting"/> so
    /// subscribers (footer + About page) see the lifecycle hop, and the
    /// returned task completes once <see cref="StartAsync"/> has handed control
    /// to the new supervisor task. Readiness is observable via
    /// <see cref="EnsureReadyAsync"/> or the next <see cref="StateChanged"/>
    /// hop into <see cref="SidecarState.Running"/>.
    /// </summary>
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SidecarHost.RestartAsync requested by user.");

        // Surface the transition for subscribers before tearing anything down.
        SetState(SidecarState.Restarting);

        // Cancel the supervisor so its loop exits even if it's mid-backoff.
        Task? supervisor;
        CancellationTokenSource? cts;
        lock (_stateLock)
        {
            supervisor = _supervisorTask;
            cts = _supervisorCts;
        }
        try { cts?.Cancel(); } catch { /* tolerate */ }

        if (supervisor is not null)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
                await Task.WhenAny(supervisor, Task.Delay(Timeout.Infinite, timeoutCts.Token))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* timeout — fall through to TerminateProcess */ }
        }

        // Make sure the python process is gone before we start a new one.
        // The JobObject stays alive — the next sidecar joins it.
        TerminateProcess();

        // Reset supervisor handles so StartAsync's "already started" guard lets
        // us spin a fresh supervisor task. _shuttingDown is already false (only
        // StopAsync sets it), but reset defensively in case a future caller
        // flips it.
        lock (_stateLock)
        {
            try { _supervisorCts?.Dispose(); } catch { /* tolerate */ }
            _supervisorCts = null;
            _supervisorTask = null;
            _shuttingDown = false;
            _port = 0;
            _lastHealthCheckSucceeded = false;
        }

        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Order matters per main-022:
        //   1. _shuttingDown first — so any supervisor iteration that races
        //      past ct.IsCancellationRequested still refuses to respawn.
        //   2. Cancel the supervisor token so blocking awaits return.
        //   3. Wait briefly for the supervisor to unwind on its own.
        //   4. Terminate the process tree (graceful tree-kill via Process,
        //      then atomic kill via JobObject as the safety net).
        //   5. Verify nothing is left alive; escalate to ERR if it is.
        _shuttingDown = true;
        SetState(SidecarState.Stopping);
        try { _supervisorCts?.Cancel(); } catch { /* tolerate */ }

        try
        {
            if (_supervisorTask is not null)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
                await Task.WhenAny(_supervisorTask, Task.Delay(Timeout.Infinite, timeoutCts.Token))
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }

        // Capture the process we spawned (if any) before TerminateProcess nulls _process,
        // so we can post-verify the OS-level exit after the JobObject is disposed.
        var spawned = _process;
        TerminateProcess();

        // Closing the JobObject triggers KILL_ON_JOB_CLOSE for everything still
        // in it — including any uvicorn worker / multiprocessing spawn that
        // tree-walking Process.Kill may have missed.
        try { _jobObject.Dispose(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error disposing sidecar JobObject."); }

        // Post-verification: if Windows hasn't reaped the process within ~1 s
        // after both kill paths fired, log at ERR per main-022 acceptance
        // criterion 3 ("if the sidecar's graceful shutdown path stalls, the
        // host still hard-kills it within ~3 s and logs the escalation at ERR").
        if (spawned is not null)
        {
            int pidForLog;
            try { pidForLog = spawned.Id; } catch { pidForLog = -1; }
            try
            {
                if (!spawned.WaitForExit(1000))
                {
                    _logger.LogError(
                        "Pocket-tts sidecar (PID {Pid}) still alive after tree-kill + JobObject close. " +
                        "Manual cleanup may be required.", pidForLog);
                }
            }
            catch (InvalidOperationException) { /* process handle no longer valid — already gone */ }
            catch (Exception ex) { _logger.LogWarning(ex, "Error verifying sidecar exit."); }
        }

        _logger.LogInformation("SidecarHost stopped.");
    }

    private async Task SuperviseAsync(CancellationToken ct)
    {
        var sidecarLogger = _loggerFactory.CreateLogger("sidecar");
        var attempt = 0;

        while (!ct.IsCancellationRequested && !_shuttingDown)
        {
            try
            {
                attempt++;
                if (!_bootstrapper.IsBootstrapped)
                {
                    SetFailed("Pocket-tts runtime is not bootstrapped — first-run dialog has not completed.");
                    return;
                }

                SetState(SidecarState.Starting);
                _port = 0;

                var psi = new ProcessStartInfo
                {
                    FileName = _bootstrapper.PythonExePath,
                    // -u forces unbuffered stdout/stderr so we observe Uvicorn's startup banner promptly.
                    // utterheim_sidecar wraps pocket_tts.main:web_app and adds /export-voice and
                    // /tts-with-state for voice cloning (ADR 0015 / main-015). Same uvicorn banner so
                    // the PortRegex below picks up the assigned port unchanged.
                    // main-039 / ADR 0024: preload English + German concurrently. The first
                    // --language is also the default for requests without an X-Voice-Language
                    // header (back-compat with the pre-main-039 single-language wire shape).
                    Arguments = "-u -m utterheim_sidecar serve --host 127.0.0.1 --port 0 --language english --language german",
                    WorkingDirectory = _paths.PythonRuntimePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                // Make sure HuggingFace cache lands in our LocalAppData (not the user's home), so
                // pocket-tts model weights live in the same place as everything else utterheim owns.
                psi.Environment["HF_HOME"] = _paths.PocketTtsModelPath;
                psi.Environment["PYTHONIOENCODING"] = "utf-8";

                using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process = process;

                var portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

                process.OutputDataReceived += (_, e) => HandleSidecarLine(sidecarLogger, e.Data, portTcs, isErr: false);
                process.ErrorDataReceived += (_, e) => HandleSidecarLine(sidecarLogger, e.Data, portTcs, isErr: true);

                if (!process.Start())
                    throw new InvalidOperationException("Failed to spawn pocket-tts sidecar.");

                // Bind the python process to our JobObject *before* it has a
                // chance to spawn workers — that way every grandchild is in
                // the job too and KILL_ON_JOB_CLOSE catches them all (main-022).
                try
                {
                    _jobObject.AssignProcess(process.Handle);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to assign sidecar (PID {Pid}) to JobObject — zombie risk on host exit.",
                        process.Id);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _logger.LogInformation("Pocket-tts sidecar started (PID {Pid}, attempt {Attempt})", process.Id, attempt);

                // Wait for the port to be reported (or for the process to die).
                using (var portTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    portTimeoutCts.CancelAfter(TimeSpan.FromMinutes(5)); // first launch may download weights
                    var port = await WaitForPortAsync(portTcs, process, portTimeoutCts.Token).ConfigureAwait(false);
                    lock (_stateLock) { _port = port; }
                    _logger.LogInformation("Pocket-tts sidecar listening on 127.0.0.1:{Port}", port);
                }

                // Health-check loop until we succeed or the process dies.
                using (var healthCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    healthCts.CancelAfter(TimeSpan.FromMinutes(5));
                    await PollHealthUntilReadyAsync(process, healthCts.Token).ConfigureAwait(false);
                }

                SetState(SidecarState.Running, healthy: true);
                _readyTcs?.TrySetResult(true);
                attempt = 0; // reset backoff once we got to running

                // Wait for the process to exit (clean shutdown or crash).
                await process.WaitForExitAsync(ct).ConfigureAwait(false);

                if (ct.IsCancellationRequested || _shuttingDown) break;

                _logger.LogWarning("Pocket-tts sidecar exited unexpectedly (code {Code}). Restarting…", process.ExitCode);
                SetState(SidecarState.Restarting, healthy: false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SidecarHost supervisor error on attempt {Attempt}", attempt);
                SetState(SidecarState.Restarting, healthy: false, error: ex.Message);
            }
            finally
            {
                TerminateProcess();
            }

            if (ct.IsCancellationRequested || _shuttingDown) break;

            // Back off: 1s, 2s, 4s, 8s, capped at 30s. After 5 attempts, fail permanently
            // so we don't pin a CPU re-spawning a broken interpreter forever.
            if (attempt >= 5)
            {
                SetFailed($"Sidecar failed to start after {attempt} attempts. Check logs.");
                return;
            }
            var delaySeconds = Math.Min(30, (int)Math.Pow(2, Math.Min(5, attempt - 1)));
            try { await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            // Reset the readiness signal so callers waiting on EnsureReadyAsync see the next attempt.
            lock (_stateLock)
            {
                _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        // Drain readiness waiters when shutting down.
        _readyTcs?.TrySetCanceled();
    }

    private async Task<int> WaitForPortAsync(TaskCompletionSource<int> portTcs, Process process, CancellationToken ct)
    {
        var exitedTask = process.WaitForExitAsync(ct);
        var portTask = portTcs.Task;
        var completed = await Task.WhenAny(portTask, exitedTask).ConfigureAwait(false);
        if (completed == exitedTask && process.HasExited)
        {
            throw new InvalidOperationException(
                $"Sidecar exited (code {process.ExitCode}) before reporting a port.");
        }
        return await portTask.ConfigureAwait(false);
    }

    private async Task PollHealthUntilReadyAsync(Process process, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var url = BaseUrl + "/health";
        while (!ct.IsCancellationRequested)
        {
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"Sidecar exited (code {process.ExitCode}) before /health became ready.");
            try
            {
                using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    lock (_stateLock) { _lastHealthCheckSucceeded = true; _lastError = null; }
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or TaskCanceledException)
            {
                // Sidecar not yet accepting connections — keep polling.
            }
            try { await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
        ct.ThrowIfCancellationRequested();
    }

    private void HandleSidecarLine(ILogger sidecarLogger, string? line, TaskCompletionSource<int> portTcs, bool isErr)
    {
        if (string.IsNullOrEmpty(line)) return;

        // Uvicorn logs to stderr by default. Both streams flow into Serilog under the same source.
        if (isErr) sidecarLogger.LogInformation("{Line}", line);
        else sidecarLogger.LogInformation("{Line}", line);

        if (!portTcs.Task.IsCompleted)
        {
            var match = PortRegex.Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
                portTcs.TrySetResult(port);
        }
    }

    private void TerminateProcess()
    {
        var process = _process;
        _process = null;
        if (process is null) return;

        try
        {
            if (!process.HasExited)
            {
                // Try graceful close first; uvicorn handles SIGINT/CTRL-C, but on Windows
                // sending CTRL_BREAK to a child without a console is unreliable. Fall back to Kill.
                try { process.CloseMainWindow(); } catch { /* tolerate */ }
                if (!process.WaitForExit(2000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error terminating sidecar process.");
        }
        finally
        {
            try { process.Dispose(); } catch { /* tolerate */ }
        }
    }

    private void SetState(SidecarState state, bool healthy = false, string? error = null)
    {
        SidecarStatus snapshot;
        lock (_stateLock)
        {
            _state = state;
            _lastHealthCheckSucceeded = healthy;
            if (error is not null) _lastError = error;
            snapshot = new SidecarStatus(_state, _lastHealthCheckSucceeded, _port, _lastError);
        }
        RaiseStateChanged(snapshot);
    }

    private void SetFailed(string error)
    {
        SidecarStatus snapshot;
        lock (_stateLock)
        {
            _state = SidecarState.Failed;
            _lastError = error;
            _lastHealthCheckSucceeded = false;
            snapshot = new SidecarStatus(_state, _lastHealthCheckSucceeded, _port, _lastError);
        }
        _logger.LogError("Sidecar permanently failed: {Error}", error);
        RaiseStateChanged(snapshot);
        _readyTcs?.TrySetException(new InvalidOperationException(error));
    }

    private void RaiseStateChanged(SidecarStatus snapshot)
    {
        try
        {
            StateChanged?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SidecarHost.StateChanged subscriber threw.");
        }
    }

    public void Dispose()
    {
        _shuttingDown = true;
        try { _supervisorCts?.Cancel(); } catch { /* tolerate */ }
        TerminateProcess();
        try { _jobObject.Dispose(); } catch { /* tolerate */ }
        _supervisorCts?.Dispose();
    }
}
