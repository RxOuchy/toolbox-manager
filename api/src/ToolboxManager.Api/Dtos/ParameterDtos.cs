using System.ComponentModel.DataAnnotations;
using ToolboxManager.Api.Models;

namespace ToolboxManager.Api.Dtos;

public sealed record ParameterDto(
    Guid Id,
    Guid ApplicationId,
    string ParameterName,
    string DisplayLabel,
    ParameterType ParameterType,
    bool IsRequired,
    string? DefaultValue,
    string Description,
    int DisplayOrder);

public sealed record CreateParameterRequest(
    [property: Required, MinLength(1), MaxLength(200)] string ParameterName,
    [property: Required, MinLength(1), MaxLength(200)] string DisplayLabel,
    ParameterType ParameterType,
    bool IsRequired,
    string? DefaultValue,
    [property: MaxLength(2000)] string Description,
    int DisplayOrder);

public sealed record UpdateParameterRequest(
    [property: Required, MinLength(1), MaxLength(200)] string ParameterName,
    [property: Required, MinLength(1), MaxLength(200)] string DisplayLabel,
    ParameterType ParameterType,
    bool IsRequired,
    string? DefaultValue,
    [property: MaxLength(2000)] string Description,
    int DisplayOrder);

public static class ParameterDtoExtensions
{
    public static ParameterDto ToDto(this ApplicationParameterEntity e) =>
        new(e.Id, e.ApplicationId, e.ParameterName, e.DisplayLabel,
            e.ParameterType, e.IsRequired, e.DefaultValue, e.Description, e.DisplayOrder);
}
