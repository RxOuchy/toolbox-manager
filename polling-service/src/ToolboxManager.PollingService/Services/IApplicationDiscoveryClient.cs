using ToolboxManager.PollingService.Models;

namespace ToolboxManager.PollingService.Services;

public interface IApplicationDiscoveryClient
{
    /// <summary>Names of all applications already registered (active and inactive).</summary>
    Task<HashSet<string>> ListApplicationNamesAsync(CancellationToken ct);

    /// <summary>
    /// POST a new application + parameters in one shot. The caller has already
    /// resolved the absolute executable path and working directory.
    /// </summary>
    Task RegisterAsync(
        ApplicationManifest manifest,
        string absoluteExecutablePath,
        string workingDirectory,
        CancellationToken ct);
}
