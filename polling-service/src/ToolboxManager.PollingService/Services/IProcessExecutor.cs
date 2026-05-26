using ToolboxManager.PollingService.Models;

namespace ToolboxManager.PollingService.Services;

public sealed record ProcessExecutionResult(
    RunStatus Status,
    int? ExitCode,
    string Stdout,
    string Stderr,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public interface IProcessExecutor
{
    /// <summary>
    /// Spawns the registered executable, captures stdout/stderr (bounded), and
    /// returns a result with status and timing. Never throws — failures are
    /// surfaced via the <see cref="ProcessExecutionResult"/>.
    /// </summary>
    Task<ProcessExecutionResult> RunAsync(SqsRunMessage request, CancellationToken ct);
}
