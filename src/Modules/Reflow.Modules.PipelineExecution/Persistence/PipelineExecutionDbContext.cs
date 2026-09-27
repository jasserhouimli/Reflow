using Microsoft.EntityFrameworkCore;
using Reflow.Modules.PipelineExecution.Domain;

namespace Reflow.Modules.PipelineExecution.Persistence;

public class PipelineExecutionDbContext : DbContext
{
    public PipelineExecutionDbContext(DbContextOptions<PipelineExecutionDbContext> options)
        : base(options) { }

    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<TaskRun> TaskRuns => Set<TaskRun>();
    public DbSet<TaskAttempt> TaskAttempts => Set<TaskAttempt>();
    public DbSet<ExecutionLog> ExecutionLogs => Set<ExecutionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pipeline_execution");

        modelBuilder.Entity<PipelineRun>(entity =>
        {
            entity.ToTable("PipelineRuns");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PipelineId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.TriggerKind).HasMaxLength(20);
            propertyError(entity);
        });

        modelBuilder.Entity<TaskRun>(entity =>
        {
            entity.ToTable("TaskRuns");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RunId);
            entity.HasIndex(e => new { e.RunId, e.Status });
            entity.HasIndex(e => e.NotBefore);
            entity.Property(e => e.NodeId).HasMaxLength(200);
            entity.Property(e => e.NodeType).HasMaxLength(200);
            entity.Property(e => e.ConfigJson).HasMaxLength(20000);
            propertyError(entity);
            entity.HasOne<PipelineRun>()
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskAttempt>(entity =>
        {
            entity.ToTable("TaskAttempts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TaskRunId);
            propertyError(entity);
            entity.HasOne<TaskRun>()
                .WithMany()
                .HasForeignKey(e => e.TaskRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExecutionLog>(entity =>
        {
            entity.ToTable("ExecutionLogs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RunId);
            entity.Property(e => e.Level).HasMaxLength(20);
            entity.Property(e => e.Message).HasMaxLength(2000);
        });
    }

    private static void propertyError<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : class
    {
        entity.Property("Error").HasMaxLength(2000);
    }
}
