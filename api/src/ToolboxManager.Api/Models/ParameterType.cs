namespace ToolboxManager.Api.Models;

public enum ParameterType
{
    String,
    Number,
    Boolean,
    Flag,
    Secret,
}

public static class ParameterTypeExtensions
{
    public static string ToDbValue(this ParameterType type) => type switch
    {
        ParameterType.String  => "string",
        ParameterType.Number  => "number",
        ParameterType.Boolean => "boolean",
        ParameterType.Flag    => "flag",
        ParameterType.Secret  => "secret",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static ParameterType FromDbValue(string value) => value switch
    {
        "string"  => ParameterType.String,
        "number"  => ParameterType.Number,
        "boolean" => ParameterType.Boolean,
        "flag"    => ParameterType.Flag,
        "secret"  => ParameterType.Secret,
        _ => throw new ArgumentException($"Unknown parameter type '{value}'.", nameof(value)),
    };
}
