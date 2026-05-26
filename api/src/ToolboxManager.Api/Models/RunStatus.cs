namespace ToolboxManager.Api.Models;

public enum RunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
}

public static class RunStatusExtensions
{
    public static string ToDbValue(this RunStatus status) => status switch
    {
        RunStatus.Queued    => "queued",
        RunStatus.Running   => "running",
        RunStatus.Succeeded => "succeeded",
        RunStatus.Failed    => "failed",
        RunStatus.Cancelled => "cancelled",
        RunStatus.TimedOut  => "timed_out",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static RunStatus FromDbValue(string value) => value switch
    {
        "queued"     => RunStatus.Queued,
        "running"    => RunStatus.Running,
        "succeeded"  => RunStatus.Succeeded,
        "failed"     => RunStatus.Failed,
        "cancelled"  => RunStatus.Cancelled,
        "timed_out"  => RunStatus.TimedOut,
        _ => throw new ArgumentException($"Unknown run status '{value}'.", nameof(value)),
    };

    public static bool IsTerminal(this RunStatus status) =>
        status is RunStatus.Succeeded or RunStatus.Failed or RunStatus.Cancelled or RunStatus.TimedOut;
}
