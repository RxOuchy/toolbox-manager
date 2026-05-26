using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ToolboxManager.PollingService.Models;

namespace ToolboxManager.PollingService.Services;

public sealed class RunStatusReporter : IRunStatusReporter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<RunStatusReporter> _logger;

    public RunStatusReporter(HttpClient http, ILogger<RunStatusReporter> logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task ReportStartedAsync(Guid runRequestId, DateTimeOffset startedAt, CancellationToken ct) =>
        SendAsync(runRequestId, new UpdatePayload
        {
            Status    = RunStatus.Running,
            StartedAt = startedAt,
        }, ct);

    public Task ReportFinishedAsync(Guid runRequestId, RunStatus status, int? exitCode, string? stdout, string? stderr, string? errorMessage, DateTimeOffset completedAt, CancellationToken ct) =>
        SendAsync(runRequestId, new UpdatePayload
        {
            Status       = status,
            ExitCode     = exitCode,
            Stdout       = stdout,
            Stderr       = stderr,
            ErrorMessage = errorMessage,
            CompletedAt  = completedAt,
        }, ct);

    private async Task SendAsync(Guid runRequestId, UpdatePayload payload, CancellationToken ct)
    {
        var url = $"/api/runs/{runRequestId}/status";
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                using var response = await _http.PatchAsJsonAsync(url, payload, JsonOpts, ct);
                if (response.IsSuccessStatusCode) return;

                _logger.LogWarning(
                    "API rejected status update for run {RunRequestId} ({Status} attempt {Attempt}): {Body}",
                    runRequestId, response.StatusCode, attempt, await response.Content.ReadAsStringAsync(ct));
            }
            catch (Exception ex) when (attempt < 5)
            {
                _logger.LogWarning(ex,
                    "API status update failed for run {RunRequestId} (attempt {Attempt}); retrying",
                    runRequestId, attempt);
            }

            // Exponential backoff with a cap.
            var delay = TimeSpan.FromMilliseconds(Math.Min(10_000, 500 * Math.Pow(2, attempt - 1)));
            await Task.Delay(delay, ct);
        }

        _logger.LogError("Giving up on status update for run {RunRequestId} after 5 attempts", runRequestId);
    }

    private sealed class UpdatePayload
    {
        public RunStatus Status { get; set; }
        public int? ExitCode { get; set; }
        public string? Stdout { get; set; }
        public string? Stderr { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }
}
