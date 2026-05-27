using Microsoft.Extensions.Options;
using ToolboxManager.PollingService.Models;
using ToolboxManager.PollingService.Options;
using ToolboxManager.PollingService.Services;

namespace ToolboxManager.PollingService;

/// <summary>
/// Periodically walks <see cref="PollingOptions.ExecutablesRoot"/> and registers
/// any applications whose README contains an "Application Settings" manifest
/// and that aren't yet in the database. Register-only: existing apps are left
/// alone (changes go through the UI).
/// </summary>
public sealed class ApplicationScanner : BackgroundService
{
    private readonly PollingOptions _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ApplicationScanner> _logger;

    public ApplicationScanner(
        IOptions<PollingOptions> options,
        IServiceScopeFactory scopes,
        ILogger<ApplicationScanner> logger)
    {
        _options = options.Value;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Application scanner started. Root={Root} IntervalSeconds={Interval}",
            _options.ExecutablesRoot, _options.AppScanIntervalSeconds);

        // Small delay so the API has a chance to be reachable on first boot.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application scan iteration failed");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_options.AppScanIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("Application scanner stopping");
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_options.ExecutablesRoot))
        {
            _logger.LogDebug("ExecutablesRoot {Root} does not exist; nothing to scan", _options.ExecutablesRoot);
            return;
        }

        using var scope = _scopes.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IApplicationDiscoveryClient>();

        HashSet<string> known;
        try
        {
            known = await client.ListApplicationNamesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list existing applications; will retry next interval");
            return;
        }

        var registered = 0;

        foreach (var dir in Directory.EnumerateDirectories(_options.ExecutablesRoot))
        {
            var readmePath = Path.Combine(dir, "README.md");
            if (!File.Exists(readmePath)) continue;

            ApplicationManifest? manifest;
            try
            {
                var readme = await File.ReadAllTextAsync(readmePath, ct);
                manifest = ManifestParser.ParseFromReadme(readme);
            }
            catch (ManifestParseException ex)
            {
                _logger.LogWarning("Skipping {Dir}: {Message}", dir, ex.Message);
                continue;
            }

            if (manifest is null) continue;

            if (string.IsNullOrWhiteSpace(manifest.Name))
            {
                _logger.LogWarning("Skipping {Dir}: manifest has no 'name'", dir);
                continue;
            }

            if (known.Contains(manifest.Name)) continue;

            var executable     = string.IsNullOrWhiteSpace(manifest.Executable) ? $"{manifest.Name}.exe" : manifest.Executable;
            var executablePath = Path.Combine(dir, executable);

            if (!File.Exists(executablePath))
            {
                _logger.LogWarning(
                    "Found manifest for {Name} in {Dir} but executable {Path} is missing; skipping registration",
                    manifest.Name, dir, executablePath);
                continue;
            }

            try
            {
                await client.RegisterAsync(manifest, executablePath, dir, ct);
                _logger.LogInformation(
                    "Auto-registered application {Name} from {Dir} with {Count} parameter(s)",
                    manifest.Name, dir, manifest.Parameters.Count);
                known.Add(manifest.Name);
                registered++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register application {Name}", manifest.Name);
            }
        }

        if (registered > 0)
            _logger.LogInformation("Scan complete: registered {Count} new application(s)", registered);
        else
            _logger.LogDebug("Scan complete: no new applications");
    }
}
