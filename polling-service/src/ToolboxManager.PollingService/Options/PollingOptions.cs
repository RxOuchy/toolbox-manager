namespace ToolboxManager.PollingService.Options;

public sealed class PollingOptions
{
    /// <summary>Base URL of the API that we report run status back to.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:8080";

    public int ApiCallbackTimeoutSeconds { get; set; } = 30;

    /// <summary>SQS long-poll wait, capped at 20 by AWS.</summary>
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>How many runs can execute concurrently on this host.</summary>
    public int MaxConcurrentRuns { get; set; } = 2;

    /// <summary>SQS message visibility timeout — should be ≥ longest expected run.</summary>
    public int VisibilityTimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// Root folder under which registered executables must live. Acts as a hard
    /// boundary so the API can't ask us to run "C:\Windows\System32\reg.exe".
    /// </summary>
    public string ExecutablesRoot { get; set; } = "C:\\ToolboxApps";
}
