namespace ToolboxManager.PollingService.Models;

/// <summary>
/// Mirror of <c>ToolboxManager.Api.Dtos.SqsRunMessage</c>. Any change here must
/// also be made on the producer side or messages will fail to deserialize.
/// </summary>
public sealed record SqsRunMessage(
    Guid RunRequestId,
    Guid ApplicationId,
    string ApplicationName,
    string ExecutablePath,
    string? WorkingDirectory,
    int TimeoutSeconds,
    IReadOnlyList<string> Arguments,
    string RequestedBy,
    DateTimeOffset QueuedAt);
