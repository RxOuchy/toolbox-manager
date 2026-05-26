using System.ComponentModel.DataAnnotations;
using ToolboxManager.Api.Models;

namespace ToolboxManager.Api.Dtos;

public sealed record ApplicationSummaryDto(
    Guid Id,
    string Name,
    string Description,
    string ExecutablePath,
    string? WorkingDirectory,
    int TimeoutSeconds,
    bool IsActive,
    int ParameterCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ApplicationDetailDto(
    Guid Id,
    string Name,
    string Description,
    string ExecutablePath,
    string? WorkingDirectory,
    int TimeoutSeconds,
    bool IsActive,
    IReadOnlyList<ParameterDto> Parameters,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateApplicationRequest(
    [property: Required, MinLength(1), MaxLength(200)] string Name,
    [property: MaxLength(2000)] string Description,
    [property: Required, MinLength(1), MaxLength(1000)] string ExecutablePath,
    [property: MaxLength(1000)] string? WorkingDirectory,
    [property: Range(1, 86400)] int TimeoutSeconds,
    IReadOnlyList<CreateParameterRequest>? Parameters);

public sealed record UpdateApplicationRequest(
    [property: Required, MinLength(1), MaxLength(200)] string Name,
    [property: MaxLength(2000)] string Description,
    [property: Required, MinLength(1), MaxLength(1000)] string ExecutablePath,
    [property: MaxLength(1000)] string? WorkingDirectory,
    [property: Range(1, 86400)] int TimeoutSeconds,
    bool IsActive);

public static class ApplicationDtoExtensions
{
    public static ApplicationSummaryDto ToSummaryDto(this ApplicationEntity e, int parameterCount) =>
        new(e.Id, e.Name, e.Description, e.ExecutablePath, e.WorkingDirectory,
            e.TimeoutSeconds, e.IsActive, parameterCount, e.CreatedAt, e.UpdatedAt);

    public static ApplicationDetailDto ToDetailDto(this ApplicationEntity e) =>
        new(e.Id, e.Name, e.Description, e.ExecutablePath, e.WorkingDirectory,
            e.TimeoutSeconds, e.IsActive,
            e.Parameters
                .OrderBy(p => p.DisplayOrder)
                .Select(ParameterDtoExtensions.ToDto)
                .ToList(),
            e.CreatedAt, e.UpdatedAt);
}
