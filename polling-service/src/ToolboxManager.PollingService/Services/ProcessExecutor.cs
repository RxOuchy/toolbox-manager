using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using ToolboxManager.PollingService.Models;
using ToolboxManager.PollingService.Options;

namespace ToolboxManager.PollingService.Services;

public sealed class ProcessExecutor : IProcessExecutor
{
    // Cap captured output to avoid runaway memory on chatty processes.
    private const int OutputCapBytes = 1_000_000;

    private readonly PollingOptions _options;
    private readonly ILogger<ProcessExecutor> _logger;

    public ProcessExecutor(IOptions<PollingOptions> options, ILogger<ProcessExecutor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProcessExecutionResult> RunAsync(SqsRunMessage request, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;

        // ── Validate the executable path is inside the allow-list root ───
        var executablePath = Path.GetFullPath(request.ExecutablePath);
        var allowedRoot    = Path.GetFullPath(_options.ExecutablesRoot);

        if (!executablePath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            var msg = $"Refusing to launch '{executablePath}' — outside allow-list root '{allowedRoot}'.";
            _logger.LogError("{Message}", msg);
            return new ProcessExecutionResult(RunStatus.Failed, null, "", "", msg, startedAt, DateTimeOffset.UtcNow);
        }

        if (!File.Exists(executablePath))
        {
            var msg = $"Executable not found at '{executablePath}'.";
            _logger.LogError("{Message}", msg);
            return new ProcessExecutionResult(RunStatus.Failed, null, "", "", msg, startedAt, DateTimeOffset.UtcNow);
        }

        var workingDir = request.WorkingDirectory ?? Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;

        var psi = new ProcessStartInfo
        {
            FileName               = executablePath,
            WorkingDirectory       = workingDir,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };

        foreach (var arg in request.Arguments)
            psi.ArgumentList.Add(arg);

        var stdout = new BoundedStringBuilder(OutputCapBytes);
        var stderr = new BoundedStringBuilder(OutputCapBytes);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            _logger.LogInformation(
                "Executing {ExecutablePath} for run {RunRequestId} (args: {Args})",
                executablePath, request.RunRequestId, string.Join(" ", request.Arguments));

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process for run {RunRequestId}", request.RunRequestId);
            return new ProcessExecutionResult(RunStatus.Failed, null, "", "", $"Failed to start process: {ex.Message}",
                startedAt, DateTimeOffset.UtcNow);
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            // Flush async readers
            process.WaitForExit();

            var completedAt = DateTimeOffset.UtcNow;
            var status = process.ExitCode == 0 ? RunStatus.Succeeded : RunStatus.Failed;

            _logger.LogInformation(
                "Run {RunRequestId} exited {ExitCode} ({Status}) in {Elapsed:F1}s",
                request.RunRequestId, process.ExitCode, status, (completedAt - startedAt).TotalSeconds);

            return new ProcessExecutionResult(status, process.ExitCode, stdout.ToString(), stderr.ToString(),
                ErrorMessage: null, startedAt, completedAt);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("Run {RunRequestId} exceeded timeout of {Timeout}s — killing", request.RunRequestId, request.TimeoutSeconds);
            TryKill(process);
            return new ProcessExecutionResult(RunStatus.TimedOut, null, stdout.ToString(), stderr.ToString(),
                $"Process killed after exceeding {request.TimeoutSeconds}s timeout.",
                startedAt, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Run {RunRequestId} cancelled (service shutting down) — killing", request.RunRequestId);
            TryKill(process);
            return new ProcessExecutionResult(RunStatus.Cancelled, null, stdout.ToString(), stderr.ToString(),
                "Process killed during service shutdown.",
                startedAt, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running {RunRequestId}", request.RunRequestId);
            TryKill(process);
            return new ProcessExecutionResult(RunStatus.Failed, null, stdout.ToString(), stderr.ToString(),
                ex.Message, startedAt, DateTimeOffset.UtcNow);
        }
    }

    private static void TryKill(Process p)
    {
        try
        {
            if (!p.HasExited) p.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort.
        }
    }

    private sealed class BoundedStringBuilder
    {
        private readonly StringBuilder _sb = new();
        private readonly int _cap;
        private bool _truncated;

        public BoundedStringBuilder(int cap) { _cap = cap; }

        public void AppendLine(string line)
        {
            if (_sb.Length >= _cap)
            {
                if (!_truncated)
                {
                    _sb.AppendLine("--- output truncated ---");
                    _truncated = true;
                }
                return;
            }
            _sb.AppendLine(line);
        }

        public override string ToString() => _sb.ToString();
    }
}
