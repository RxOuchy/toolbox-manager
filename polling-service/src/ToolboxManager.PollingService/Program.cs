using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Extensions.Logging;
using ToolboxManager.PollingService;
using ToolboxManager.PollingService.Options;
using ToolboxManager.PollingService.Services;

var logger = NLog.LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config")
    .GetCurrentClassLogger();

try
{
    logger.Info("Polling Service starting up");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();

    builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection("Aws"));
    builder.Services.Configure<PollingOptions>(builder.Configuration.GetSection("Polling"));

    // ── AWS SQS client ─────────────────────────────────────────
    builder.Services.AddSingleton<IAmazonSQS>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
        var creds = new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey);
        var config = new AmazonSQSConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(opts.Region),
        };
        if (!string.IsNullOrWhiteSpace(opts.SqsServiceUrl))
        {
            config.ServiceURL = opts.SqsServiceUrl;
            config.UseHttp = opts.SqsServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }
        return new AmazonSQSClient(creds, config);
    });

    // ── HttpClient for API callbacks ───────────────────────────
    builder.Services.AddHttpClient<IRunStatusReporter, RunStatusReporter>((sp, http) =>
    {
        var opts = sp.GetRequiredService<IOptions<PollingOptions>>().Value;
        http.BaseAddress = new Uri(opts.ApiBaseUrl);
        http.Timeout = TimeSpan.FromSeconds(opts.ApiCallbackTimeoutSeconds);
    });

    builder.Services.AddHttpClient<IApplicationDiscoveryClient, ApplicationDiscoveryClient>((sp, http) =>
    {
        var opts = sp.GetRequiredService<IOptions<PollingOptions>>().Value;
        http.BaseAddress = new Uri(opts.ApiBaseUrl);
        http.Timeout = TimeSpan.FromSeconds(opts.ApiCallbackTimeoutSeconds);
    });

    builder.Services.AddSingleton<IProcessExecutor, ProcessExecutor>();
    builder.Services.AddHostedService<Worker>();
    builder.Services.AddHostedService<ApplicationScanner>();

    // Run as a Windows Service when launched from the Service Control Manager.
    builder.Services.AddWindowsService(o => o.ServiceName = "ToolboxManagerPollingService");

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "Polling Service terminated unexpectedly");
    throw;
}
finally
{
    NLog.LogManager.Shutdown();
}
