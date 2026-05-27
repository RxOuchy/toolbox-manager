using System.Text.Json;
using System.Text.RegularExpressions;
using ToolboxManager.PollingService.Models;

namespace ToolboxManager.PollingService.Services;

public sealed class ManifestParseException : Exception
{
    public ManifestParseException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Extracts an <see cref="ApplicationManifest"/> from a README's
/// "Application Settings" section. Format:
///
///   ## Application Settings
///
///   ```json
///   { "name": "...", "parameters": [ ... ] }
///   ```
///
/// The heading is matched case-insensitively at any depth (h1-h6). The first
/// fenced code block after the heading is parsed as JSON; the language tag
/// (json) is optional.
/// </summary>
public static class ManifestParser
{
    private static readonly Regex HeadingRegex = new(
        @"^\s*#{1,6}\s*application\s+settings\s*#*\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FenceOpenRegex = new(
        @"^\s*```\s*([A-Za-z0-9_+-]*)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex FenceCloseRegex = new(
        @"^\s*```\s*$",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Returns the parsed manifest, or <c>null</c> if the README has no
    /// "Application Settings" section. Throws <see cref="ManifestParseException"/>
    /// when the section exists but its content is malformed.
    /// </summary>
    public static ApplicationManifest? ParseFromReadme(string readmeContent)
    {
        var lines = readmeContent.Replace("\r\n", "\n").Split('\n');

        var headingIndex = FindLine(lines, 0, HeadingRegex);
        if (headingIndex < 0) return null;

        var fenceOpenIndex = FindLine(lines, headingIndex + 1, FenceOpenRegex);
        if (fenceOpenIndex < 0)
            throw new ManifestParseException(
                "Found 'Application Settings' heading but no fenced code block follows it.");

        var fenceCloseIndex = FindLine(lines, fenceOpenIndex + 1, FenceCloseRegex);
        if (fenceCloseIndex < 0)
            throw new ManifestParseException(
                "Code block opened after 'Application Settings' heading but is never closed.");

        var json = string.Join('\n', lines.Skip(fenceOpenIndex + 1).Take(fenceCloseIndex - fenceOpenIndex - 1));

        ApplicationManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ApplicationManifest>(json, JsonOpts);
        }
        catch (JsonException ex)
        {
            throw new ManifestParseException($"Application Settings JSON is invalid: {ex.Message}", ex);
        }

        if (manifest is null)
            throw new ManifestParseException("Application Settings JSON deserialized to null.");

        return manifest;
    }

    private static int FindLine(string[] lines, int startIndex, Regex pattern)
    {
        for (var i = startIndex; i < lines.Length; i++)
            if (pattern.IsMatch(lines[i])) return i;
        return -1;
    }
}
