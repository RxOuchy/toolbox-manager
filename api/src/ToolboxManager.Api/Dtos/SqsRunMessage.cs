namespace ToolboxManager.Api.Dtos;

/// <summary>
/// Wire contract between the API (producer) and the Polling Service (consumer).
/// Any change here must be made in the Polling Service's mirror type. Keep the
/// shape minimal; the consumer re-queries the API for anything else it needs.
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
