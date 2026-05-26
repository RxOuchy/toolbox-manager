namespace ToolboxManager.PollingService.Options;

public sealed class AwsOptions
{
    public string Region { get; set; } = "us-east-1";
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string SqsQueueName { get; set; } = "toolbox-run-requests";
    public string? SqsServiceUrl { get; set; }
}
