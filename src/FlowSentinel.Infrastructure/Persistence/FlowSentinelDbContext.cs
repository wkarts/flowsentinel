using Microsoft.EntityFrameworkCore;

namespace FlowSentinel.Infrastructure.Persistence;

internal sealed class FlowSentinelDbContext : DbContext
{
    public FlowSentinelDbContext(DbContextOptions<FlowSentinelDbContext> options) : base(options)
    {
    }

    public DbSet<AutomationEntity> Automations => Set<AutomationEntity>();
    public DbSet<OccurrenceEntity> Occurrences => Set<OccurrenceEntity>();
    public DbSet<DeliveryEntity> Deliveries => Set<DeliveryEntity>();
    public DbSet<ChannelConfigurationEntity> ChannelConfigurations => Set<ChannelConfigurationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AutomationEntity>(entity =>
        {
            entity.ToTable("automations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DefinitionJson).IsRequired();
            entity.HasIndex(x => new { x.Enabled, x.NextRunAt });
        });

        modelBuilder.Entity<OccurrenceEntity>(entity =>
        {
            entity.ToTable("occurrences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RecordKey).HasMaxLength(500).IsRequired();
            entity.Property(x => x.SnapshotJson).IsRequired();
            entity.HasIndex(x => new { x.AutomationId, x.RecordKey, x.Status });
        });

        modelBuilder.Entity<DeliveryEntity>(entity =>
        {
            entity.ToTable("deliveries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Recipient).HasMaxLength(500).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.Status, x.DueAt });
            entity.HasIndex(x => new { x.OccurrenceId, x.ActionId, x.ExecutionNumber });
        });

        modelBuilder.Entity<ChannelConfigurationEntity>(entity =>
        {
            entity.ToTable("channel_configurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SettingsJson).IsRequired();
        });
    }
}
