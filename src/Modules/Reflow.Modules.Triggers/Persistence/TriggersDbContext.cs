using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Triggers.Domain;

namespace Reflow.Modules.Triggers.Persistence;

public class TriggersDbContext : DbContext
{
    public TriggersDbContext(DbContextOptions<TriggersDbContext> options) : base(options) { }

    public DbSet<Trigger> Triggers => Set<Trigger>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("triggers");

        modelBuilder.Entity<Trigger>(entity =>
        {
            entity.ToTable("Triggers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Kind, e.IsEnabled, e.NextRunAt });
            entity.HasIndex(e => e.SecretTokenHash);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Cron).HasMaxLength(100);
            entity.Property(e => e.Timezone).HasMaxLength(100);
            entity.Property(e => e.SecretTokenHash).HasMaxLength(64);
        });

        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("WebhookEvents");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TriggerId);
            entity.HasIndex(e => new { e.TriggerId, e.ExternalEventId }).IsUnique();
            entity.Property(e => e.ExternalEventId).HasMaxLength(200);
            entity.Property(e => e.PayloadHash).IsRequired().HasMaxLength(64);
            entity.HasOne<Trigger>()
                .WithMany()
                .HasForeignKey(e => e.TriggerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
