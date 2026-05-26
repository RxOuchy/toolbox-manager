using ToolboxManager.Api.Dtos;

namespace ToolboxManager.Api.Services;

public interface ISqsPublisher
{
    /// <summary>Publishes a run request to SQS. Returns the message ID.</summary>
    Task<string> PublishRunAsync(SqsRunMessage message, CancellationToken ct = default);
}
