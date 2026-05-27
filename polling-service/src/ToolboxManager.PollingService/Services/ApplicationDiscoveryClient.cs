using System.Net.Http.Json;
using System.Text.Json;
using ToolboxManager.PollingService.Models;

namespace ToolboxManager.PollingService.Services;

public sealed class ApplicationDiscoveryClient : IApplicationDiscoveryClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger<ApplicationDiscoveryClient> _logger;

    public ApplicationDiscoveryClient(HttpClient http, ILogger<ApplicationDiscoveryClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<HashSet<string>> ListApplicationNamesAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("/api/applications?includeInactive=true", ct);
        response.EnsureSuccessStatusCode();

        var apps = await response.Content.ReadFromJsonAsync<List<ApiApplicationSummary>>(JsonOpts, ct);
        return apps?.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
               ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task RegisterAsync(
        ApplicationManifest manifest,
        string absoluteExecutablePath,
        string workingDirectory,
        CancellationToken ct)
    {
        var payload = new
        {
            name             = manifest.Name,
            description      = manifest.Description ?? string.Empty,
            executablePath   = absoluteExecutablePath,
            workingDirectory = string.IsNullOrWhiteSpace(manifest.WorkingDirectory)
                                 ? workingDirectory
                                 : manifest.WorkingDirectory,
            timeoutSeconds   = manifest.TimeoutSeconds <= 0 ? 300 : manifest.TimeoutSeconds,
            parameters       = manifest.Parameters.Select((p, idx) => new
            {
                parameterName  = p.Name,
                displayLabel   = string.IsNullOrWhiteSpace(p.Label) ? p.Name : p.Label,
                parameterType  = NormalizeType(p.Type),
                isRequired     = p.Required,
                defaultValue   = p.Default,
                description    = p.Description ?? string.Empty,
                displayOrder   = p.Order > 0 ? p.Order : idx + 1,
            }).ToArray(),
        };

        using var response = await _http.PostAsJsonAsync("/api/applications", payload, JsonOpts, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"API rejected registration of '{manifest.Name}': {(int)response.StatusCode} {response.ReasonPhrase} -- {body}");
        }
    }

    private static string NormalizeType(string? type) =>
        (type ?? "string").Trim().ToLowerInvariant() switch
        {
            "string"  => "String",
            "number"  => "Number",
            "boolean" => "Boolean",
            "flag"    => "Flag",
            "secret"  => "Secret",
            _ => throw new InvalidOperationException(
                $"Unknown parameter type '{type}'. Expected: string | number | boolean | flag | secret."),
        };

    private sealed record ApiApplicationSummary(string Name);
}
