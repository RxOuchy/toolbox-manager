namespace ToolboxManager.PollingService.Models;

/// <summary>Mirror of the API's RunStatus enum; serialized as the same names.</summary>
public enum RunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
}
