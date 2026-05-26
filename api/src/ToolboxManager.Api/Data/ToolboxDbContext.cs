using Microsoft.EntityFrameworkCore;
using ToolboxManager.Api.Models;

namespace ToolboxManager.Api.Data;

public sealed class ToolboxDbContext : DbContext
{
    public ToolboxDbContext(DbContextOptions<ToolboxDbContext> options) : base(options) { }

    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();
    public DbSet<ApplicationParameterEntity> ApplicationParameters => Set<ApplicationParameterEntity>();
    public DbSet<RunRequestEntity> RunRequests => Set<RunRequestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── applications ────────────────────────────────────
        modelBuilder.Entity<ApplicationEntity>(e =>
        {
            e.ToTable("applications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").IsRequired();
            e.Property(x => x.ExecutablePath).HasColumnName("executable_path").HasMaxLength(1000).IsRequired();
            e.Property(x => x.WorkingDirectory).HasColumnName("working_directory").HasMaxLength(1000);
            e.Property(x => x.TimeoutSeconds).HasColumnName("timeout_seconds");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.Name).IsUnique();

            e.HasMany(x => x.Parameters)
                .WithOne(p => p.Application)
                .HasForeignKey(p => p.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── application_parameters ──────────────────────────
        modelBuilder.Entity<ApplicationParameterEntity>(e =>
        {
            e.ToTable("application_parameters");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ApplicationId).HasColumnName("application_id");
            e.Property(x => x.ParameterName).HasColumnName("parameter_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayLabel).HasColumnName("display_label").HasMaxLength(200).IsRequired();
            e.Property(x => x.ParameterType)
                .HasColumnName("parameter_type")
                .HasMaxLength(20)
                .HasConversion(v => v.ToDbValue(), v => ParameterTypeExtensions.FromDbValue(v))
                .IsRequired();
            e.Property(x => x.IsRequired).HasColumnName("is_required");
            e.Property(x => x.DefaultValue).HasColumnName("default_value");
            e.Property(x => x.Description).HasColumnName("description").IsRequired();
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.ApplicationId, x.ParameterName }).IsUnique();
        });

        // ── run_requests ────────────────────────────────────
        modelBuilder.Entity<RunRequestEntity>(e =>
        {
            e.ToTable("run_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ApplicationId).HasColumnName("application_id");
            e.Property(x => x.RequestedBy).HasColumnName("requested_by").HasMaxLength(320).IsRequired();
            e.Property(x => x.ParameterValuesJson).HasColumnName("parameter_values").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(v => v.ToDbValue(), v => RunStatusExtensions.FromDbValue(v))
                .IsRequired();
            e.Property(x => x.SqsMessageId).HasColumnName("sqs_message_id").HasMaxLength(200);
            e.Property(x => x.ExitCode).HasColumnName("exit_code");
            e.Property(x => x.Stdout).HasColumnName("stdout");
            e.Property(x => x.Stderr).HasColumnName("stderr");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
            e.Property(x => x.QueuedAt).HasColumnName("queued_at");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(x => x.Application)
                .WithMany()
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
