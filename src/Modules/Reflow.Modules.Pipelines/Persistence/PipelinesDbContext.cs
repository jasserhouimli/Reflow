using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Domain;

namespace Reflow.Modules.Pipelines.Persistence;

public class PipelinesDbContext : DbContext
{
    public PipelinesDbContext(DbContextOptions<PipelinesDbContext> options) : base(options) { }

    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<PipelineNode> PipelineNodes => Set<PipelineNode>();
    public DbSet<PipelineEdge> PipelineEdges => Set<PipelineEdge>();
    public DbSet<PipelineVersion> PipelineVersions => Set<PipelineVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pipelines");

        modelBuilder.Entity<Pipeline>(entity =>
        {
            entity.ToTable("Pipelines");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OwnerId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<PipelineNode>(entity =>
        {
            entity.ToTable("PipelineNodes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PipelineId);
            entity.Property(e => e.NodeId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NodeType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ConfigJson).IsRequired().HasMaxLength(20000);
            entity.HasOne<Pipeline>()
                .WithMany(p => p.Nodes)
                .HasForeignKey(e => e.PipelineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PipelineEdge>(entity =>
        {
            entity.ToTable("PipelineEdges");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PipelineId);
            entity.Property(e => e.SourceNodeId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TargetNodeId).IsRequired().HasMaxLength(200);
            entity.HasOne<Pipeline>()
                .WithMany(p => p.Edges)
                .HasForeignKey(e => e.PipelineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PipelineVersion>(entity =>
        {
            entity.ToTable("PipelineVersions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PipelineId, e.VersionNumber }).IsUnique();
            entity.Property(e => e.DefinitionJson).IsRequired();
        });
    }
}
