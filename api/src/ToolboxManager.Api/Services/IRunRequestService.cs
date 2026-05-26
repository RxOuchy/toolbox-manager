using ToolboxManager.Api.Dtos;

namespace ToolboxManager.Api.Services;

public interface IRunRequestService
{
    /// <summary>
    /// Validates parameter values against the application's parameter definitions,
    /// persists a run_request row, builds the argv, and publishes to SQS.
    /// </summary>
    Task<RunRequestDto> TriggerRunAsync(Guid applicationId, TriggerRunRequest request, CancellationToken ct = default);
}
