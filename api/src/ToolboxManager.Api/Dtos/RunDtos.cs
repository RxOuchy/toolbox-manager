using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ToolboxManager.Api.Models;

namespace ToolboxManager.Api.Dtos;

public sealed record RunRequestDto(
    Guid Id,
    Guid ApplicationId,
    string ApplicationName,
    string RequestedBy,
    IReadOnlyDictionary<string, string?> ParameterValues,
    RunStatus Status,
    int? ExitCode,
    string? Stdout,
    string? Stderr,
    string? ErrorMessage,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record TriggerRunRequest(
    [property: Required, EmailAddress, MaxLength(320)] string RequestedBy,
    IReadOnlyDictionary<string, string?> ParameterValues);

public sealed record UpdateRunStatusRequest(
    RunStatus Status,
    int? ExitCode,
    string? Stdout,
    string? Stderr,
    string? ErrorMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public static class RunDtoExtensions
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static RunRequestDto ToDto(this RunRequestEntity e)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(
            e.ParameterValuesJson, JsonOpts) ?? new();

        return new RunRequestDto(
            e.Id, e.ApplicationId, e.Application?.Name ?? string.Empty, e.RequestedBy,
            values, e.Status, e.ExitCode, e.Stdout, e.Stderr, e.ErrorMessage,
            e.QueuedAt, e.StartedAt, e.CompletedAt);
    }
}
