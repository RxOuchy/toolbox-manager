namespace ToolboxManager.PollingService.Models;

/// <summary>
/// Shape of the JSON manifest embedded in each application's README under the
/// <c>## Application Settings</c> heading. Used by the discovery scanner to
/// auto-register tools dropped onto the polling host.
/// </summary>
public sealed class ApplicationManifest
{
    /// <summary>Unique name of the application. Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human-friendly summary surfaced in the UI.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Executable filename relative to the manifest's directory. Optional;
    /// defaults to <c>{Name}.exe</c>.
    /// </summary>
    public string? Executable { get; set; }

    /// <summary>
    /// Working directory for the process. Optional; defaults to the directory
    /// the manifest was found in.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Max wall-clock runtime. Default 300s if unset.</summary>
    public int TimeoutSeconds { get; set; }

    public List<ParameterManifest> Parameters { get; set; } = new();
}

public sealed class ParameterManifest
{
    /// <summary>Argument flag passed on the command line, e.g. "--config".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Form label shown in the UI. Defaults to <see cref="Name"/> if unset.</summary>
    public string? Label { get; set; }

    /// <summary>string | number | boolean | flag | secret (case-insensitive).</summary>
    public string Type { get; set; } = "string";

    public bool Required { get; set; }
    public string? Default { get; set; }
    public string? Description { get; set; }

    /// <summary>UI display order. Defaults to the array index if unset.</summary>
    public int Order { get; set; }
}
