namespace ToolboxManager.Api.Models;

public sealed class RunRequestEntity
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>JSON document of {parameterName: value}. Stored as jsonb.</summary>
    public string ParameterValuesJson { get; set; } = "{}";

    public RunStatus Status { get; set; } = RunStatus.Queued;
    public string? SqsMessageId { get; set; }
    public int? ExitCode { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ApplicationEntity? Application { get; set; }
}
