using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using ToolboxManager.PollingService.Models;
using ToolboxManager.PollingService.Options;
using ToolboxManager.PollingService.Services;

namespace ToolboxManager.PollingService;

public sealed class Worker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IAmazonSQS _sqs;
    private readonly IProcessExecutor _executor;
    private readonly IServiceScopeFactory _scopes;
    private readonly AwsOptions _aws;
    private readonly PollingOptions _polling;
    private readonly ILogger<Worker> _logger;

    private string? _queueUrl;
    private readonly SemaphoreSlim _concurrencyGate;

    public Worker(
        IAmazonSQS sqs,
        IProcessExecutor executor,
        IServiceScopeFactory scopes,
        IOptions<AwsOptions> aws,
        IOptions<PollingOptions> polling,
        ILogger<Worker> logger)
    {
        _sqs = sqs;
        _executor = executor;
        _scopes = scopes;
        _aws = aws.Value;
        _polling = polling.Value;
        _logger = logger;
        _concurrencyGate = new SemaphoreSlim(_polling.MaxConcurrentRuns, _polling.MaxConcurrentRuns);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Polling Service started. Queue={QueueName} Region={Region} MaxConcurrent={MaxConcurrent} ExecutablesRoot={Root}",
            _aws.SqsQueueName, _aws.Region, _polling.MaxConcurrentRuns, _polling.ExecutablesRoot);

        _queueUrl = await ResolveQueueUrlAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _concurrencyGate.WaitAsync(stoppingToken);

                ReceiveMessageResponse response;
                try
                {
                    response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl                  = _queueUrl,
                        MaxNumberOfMessages       = 1,
                        WaitTimeSeconds           = _polling.WaitTimeSeconds,
                        VisibilityTimeout         = _polling.VisibilityTimeoutSeconds,
                        MessageSystemAttributeNames = new List<string> { "ApproximateReceiveCount" },
                    }, stoppingToken);
                }
                catch
                {
                    _concurrencyGate.Release();
                    throw;
                }

                if (response.Messages.Count == 0)
                {
                    _concurrencyGate.Release();
                    continue;
                }

                // Fire-and-forget: handler is responsible for releasing the gate
                // and deleting the SQS message on terminal outcome.
                _ = Task.Run(() => HandleMessageAsync(response.Messages[0], stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling loop iteration failed; backing off");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Polling Service stopping");
    }

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        try
        {
            SqsRunMessage? run;
            try
            {
                run = JsonSerializer.Deserialize<SqsRunMessage>(message.Body, JsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discarding malformed message {MessageId}: {Body}", message.MessageId, message.Body);
                await DeleteMessageAsync(message, ct);
                return;
            }

            if (run is null)
            {
                _logger.LogWarning("Discarding null-deserialized message {MessageId}", message.MessageId);
                await DeleteMessageAsync(message, ct);
                return;
            }

            using var scope = _scopes.CreateScope();
            var reporter = scope.ServiceProvider.GetRequiredService<IRunStatusReporter>();

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["RunRequestId"]    = run.RunRequestId,
                ["ApplicationName"] = run.ApplicationName,
            }))
            {
                await reporter.ReportStartedAsync(run.RunRequestId, DateTimeOffset.UtcNow, ct);

                var result = await _executor.RunAsync(run, ct);

                await reporter.ReportFinishedAsync(
                    run.RunRequestId,
                    result.Status,
                    result.ExitCode,
                    result.Stdout,
                    result.Stderr,
                    result.ErrorMessage,
                    result.CompletedAt,
                    ct);
            }

            await DeleteMessageAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled error processing message {MessageId} — leaving on queue for retry / DLQ",
                message.MessageId);
            // Do NOT delete — let SQS redeliver after visibility timeout. DLQ
            // (configured on the queue) catches the poison after MaxReceiveCount.
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    private async Task DeleteMessageAsync(Message message, CancellationToken ct)
    {
        try
        {
            await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message {MessageId} from queue", message.MessageId);
        }
    }

    private async Task<string> ResolveQueueUrlAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var resp = await _sqs.GetQueueUrlAsync(_aws.SqsQueueName, ct);
                _logger.LogInformation("Resolved queue URL: {QueueUrl}", resp.QueueUrl);
                return resp.QueueUrl;
            }
            catch (QueueDoesNotExistException)
            {
                _logger.LogWarning("Queue {QueueName} doesn't exist yet — waiting", _aws.SqsQueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve queue URL — retrying");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
        throw new OperationCanceledException(ct);
    }

    public override void Dispose()
    {
        _concurrencyGate.Dispose();
        base.Dispose();
    }
}
