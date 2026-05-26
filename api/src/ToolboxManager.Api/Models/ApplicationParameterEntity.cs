namespace ToolboxManager.Api.Models;

public sealed class ApplicationParameterEntity
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public ParameterType ParameterType { get; set; }
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ApplicationEntity? Application { get; set; }
}
