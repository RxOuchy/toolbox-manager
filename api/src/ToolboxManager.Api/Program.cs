using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using ToolboxManager.Api.Data;
using ToolboxManager.Api.Options;
using ToolboxManager.Api.Services;

var logger = NLog.LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config")
    .GetCurrentClassLogger();

try
{
    logger.Info("Toolbox Manager API starting up");

    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // ── Configuration ─────────────────────────────────────────
    builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection("Aws"));
    builder.Services.Configure<SeqOptions>(builder.Configuration.GetSection("Seq"));

    // ── EF Core / PostgreSQL ──────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

    builder.Services.AddDbContext<ToolboxDbContext>(opts =>
        opts.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

    // ── AWS SQS ───────────────────────────────────────────────
    builder.Services.AddSingleton<IAmazonSQS>(sp =>
    {
        var opts = builder.Configuration.GetSection("Aws").Get<AwsOptions>()!;
        var credentials = new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey);
        var config = new AmazonSQSConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(opts.Region),
        };
        if (!string.IsNullOrWhiteSpace(opts.SqsServiceUrl))
        {
            // LocalStack or any non-AWS SQS-compatible endpoint.
            config.ServiceURL = opts.SqsServiceUrl;
            config.UseHttp = opts.SqsServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }
        return new AmazonSQSClient(credentials, config);
    });

    builder.Services.AddSingleton<ISqsPublisher, SqsPublisher>();
    builder.Services.AddScoped<IRunRequestService, RunRequestService>();
    builder.Services.AddScoped<MigrationRunner>();

    // ── MVC / Swagger ─────────────────────────────────────────
    builder.Services
        .AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title   = "Toolbox Manager API",
            Version = "v1",
            Description = "CRUD for registered console apps + run dispatching via SQS."
        });
    });

    builder.Services.AddCors(opts =>
    {
        opts.AddDefaultPolicy(p => p
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ToolboxDbContext>("postgres");

    var app = builder.Build();

    // ── Migrations ────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
        await runner.ApplyAsync();
    }

    // ── HTTP pipeline ─────────────────────────────────────────
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Toolbox Manager API v1"));

    app.UseCors();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "API terminated unexpectedly");
    throw;
}
finally
{
    NLog.LogManager.Shutdown();
}
