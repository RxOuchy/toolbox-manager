namespace ToolboxManager.Api.Options;

public sealed class AwsOptions
{
    public string Region { get; set; } = "us-east-1";
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string SqsQueueName { get; set; } = "toolbox-run-requests";

    /// <summary>
    /// Override the SQS endpoint (e.g. http://localstack:4566). Leave empty in
    /// production to use the real AWS endpoint derived from <see cref="Region"/>.
    /// </summary>
    public string? SqsServiceUrl { get; set; }
}
