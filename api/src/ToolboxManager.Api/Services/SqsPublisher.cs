using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using ToolboxManager.Api.Dtos;
using ToolboxManager.Api.Options;

namespace ToolboxManager.Api.Services;

public sealed class SqsPublisher : ISqsPublisher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _options;
    private readonly ILogger<SqsPublisher> _logger;
    private string? _cachedQueueUrl;

    public SqsPublisher(IAmazonSQS sqs, IOptions<AwsOptions> options, ILogger<SqsPublisher> logger)
    {
        _sqs = sqs;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> PublishRunAsync(SqsRunMessage message, CancellationToken ct = default)
    {
        var queueUrl = await ResolveQueueUrlAsync(ct);
        var payload  = JsonSerializer.Serialize(message, JsonOpts);

        var response = await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl    = queueUrl,
            MessageBody = payload,
        }, ct);

        _logger.LogInformation(
            "Published run {RunRequestId} for app {ApplicationName} to SQS ({MessageId})",
            message.RunRequestId, message.ApplicationName, response.MessageId);

        return response.MessageId;
    }

    private async Task<string> ResolveQueueUrlAsync(CancellationToken ct)
    {
        if (_cachedQueueUrl is not null) return _cachedQueueUrl;

        try
        {
            var resp = await _sqs.GetQueueUrlAsync(_options.SqsQueueName, ct);
            _cachedQueueUrl = resp.QueueUrl;
            return _cachedQueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            _logger.LogWarning("Queue {QueueName} not found, creating it.", _options.SqsQueueName);
            var created = await _sqs.CreateQueueAsync(_options.SqsQueueName, ct);
            _cachedQueueUrl = created.QueueUrl;
            return _cachedQueueUrl;
        }
    }
}
