using ToolboxManager.PollingService.Models;

namespace ToolboxManager.PollingService.Services;

public interface IRunStatusReporter
{
    Task ReportStartedAsync(Guid runRequestId, DateTimeOffset startedAt, CancellationToken ct);
    Task ReportFinishedAsync(Guid runRequestId, RunStatus status, int? exitCode, string? stdout, string? stderr, string? errorMessage, DateTimeOffset completedAt, CancellationToken ct);
}
