using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ToolboxManager.Api.Data;

namespace ToolboxManager.Api.Services;

/// <summary>
/// Lightweight migration runner. Each *.sql file in /Migrations is applied
/// once and recorded in a <c>schema_migrations</c> tracking table.
/// </summary>
public sealed class MigrationRunner
{
    private const string MigrationsFolder = "Migrations";

    private readonly ToolboxDbContext _db;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(ToolboxDbContext db, ILogger<MigrationRunner> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken ct = default)
    {
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        try
        {
            await EnsureTrackingTableAsync(conn, ct);

            var migrationsPath = Path.Combine(AppContext.BaseDirectory, MigrationsFolder);
            if (!Directory.Exists(migrationsPath))
            {
                _logger.LogWarning("Migrations folder {Path} not found - skipping.", migrationsPath);
                return;
            }

            var files = Directory.EnumerateFiles(migrationsPath, "V*.sql")
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var applied = await GetAppliedVersionsAsync(conn, ct);

            foreach (var file in files)
            {
                var version = Path.GetFileNameWithoutExtension(file);
                if (applied.Contains(version))
                {
                    _logger.LogDebug("Migration {Version} already applied.", version);
                    continue;
                }

                _logger.LogInformation("Applying migration {Version}", version);
                var sql = await File.ReadAllTextAsync(file, ct);

                await using var tx = await conn.BeginTransactionAsync(ct);

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO schema_migrations (version, applied_at) VALUES (@v, now())";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "v";
                    p.Value = version;
                    cmd.Parameters.Add(p);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                _logger.LogInformation("Applied migration {Version}", version);
            }
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    private static async Task EnsureTrackingTableAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version    varchar(200) PRIMARY KEY,
                applied_at timestamptz  NOT NULL DEFAULT now()
            );";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<HashSet<string>> GetAppliedVersionsAsync(DbConnection conn, CancellationToken ct)
    {
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT version FROM schema_migrations";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            applied.Add(reader.GetString(0));

        return applied;
    }
}
