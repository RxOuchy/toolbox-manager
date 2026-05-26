using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ToolboxManager.Api.Data;
using ToolboxManager.Api.Dtos;
using ToolboxManager.Api.Models;

namespace ToolboxManager.Api.Services;

public sealed class RunRequestService : IRunRequestService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ToolboxDbContext _db;
    private readonly ISqsPublisher _publisher;
    private readonly ILogger<RunRequestService> _logger;

    public RunRequestService(ToolboxDbContext db, ISqsPublisher publisher, ILogger<RunRequestService> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<RunRequestDto> TriggerRunAsync(Guid applicationId, TriggerRunRequest request, CancellationToken ct = default)
    {
        var application = await _db.Applications
            .Include(a => a.Parameters)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw new KeyNotFoundException($"Application {applicationId} not found.");

        if (!application.IsActive)
            throw new InvalidOperationException($"Application '{application.Name}' is disabled.");

        var values = request.ParameterValues ?? new Dictionary<string, string?>();
        var arguments = BuildArguments(application, values);

        // Persist
        var now = DateTimeOffset.UtcNow;
        var entity = new RunRequestEntity
        {
            Id                  = Guid.NewGuid(),
            ApplicationId       = application.Id,
            RequestedBy         = request.RequestedBy,
            ParameterValuesJson = JsonSerializer.Serialize(values, JsonOpts),
            Status              = RunStatus.Queued,
            QueuedAt            = now,
            CreatedAt           = now,
            UpdatedAt           = now,
        };
        _db.RunRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Publish to SQS
        var sqsMessage = new SqsRunMessage(
            entity.Id,
            application.Id,
            application.Name,
            application.ExecutablePath,
            application.WorkingDirectory,
            application.TimeoutSeconds,
            arguments,
            entity.RequestedBy,
            entity.QueuedAt);

        try
        {
            var messageId = await _publisher.PublishRunAsync(sqsMessage, ct);
            entity.SqsMessageId = messageId;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish run {RunRequestId} to SQS, marking as Failed.", entity.Id);
            entity.Status = RunStatus.Failed;
            entity.ErrorMessage = $"Failed to queue: {ex.Message}";
            entity.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        entity.Application = application;
        return entity.ToDto();
    }

    private static IReadOnlyList<string> BuildArguments(
        ApplicationEntity application,
        IReadOnlyDictionary<string, string?> values)
    {
        var ordered = application.Parameters.OrderBy(p => p.DisplayOrder).ToList();
        var args = new List<string>();
        var missing = new List<string>();

        foreach (var p in ordered)
        {
            values.TryGetValue(p.ParameterName, out var raw);
            var value = raw ?? p.DefaultValue;

            if (p.IsRequired && string.IsNullOrEmpty(value) && p.ParameterType != ParameterType.Flag)
            {
                missing.Add(p.ParameterName);
                continue;
            }

            switch (p.ParameterType)
            {
                case ParameterType.Flag:
                    if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                        args.Add(p.ParameterName);
                    break;

                case ParameterType.Boolean:
                    if (!string.IsNullOrEmpty(value))
                    {
                        args.Add(p.ParameterName);
                        args.Add(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false");
                    }
                    break;

                case ParameterType.Number:
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (!double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                            throw new ArgumentException($"Parameter '{p.ParameterName}' must be a number; got '{value}'.");
                        args.Add(p.ParameterName);
                        args.Add(value);
                    }
                    break;

                case ParameterType.String:
                case ParameterType.Secret:
                    if (!string.IsNullOrEmpty(value))
                    {
                        args.Add(p.ParameterName);
                        args.Add(value);
                    }
                    break;
            }
        }

        if (missing.Count > 0)
            throw new ArgumentException($"Missing required parameter(s): {string.Join(", ", missing)}.");

        return args;
    }
}
