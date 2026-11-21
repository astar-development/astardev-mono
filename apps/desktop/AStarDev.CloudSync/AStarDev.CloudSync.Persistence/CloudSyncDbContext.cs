using AStarDev.CloudSync.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The EF Core view of the SQLite database. Only Persistence uses it: the UI, providers and Domain never see it.</summary>
internal sealed class CloudSyncDbContext(DbContextOptions<CloudSyncDbContext> options) : DbContext(options)
{
    /// <summary>Connected accounts.</summary>
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

    /// <summary>Configured sync relationships.</summary>
    public DbSet<SyncJobEntity> SyncJobs => Set<SyncJobEntity>();

    /// <summary>Files and folders under sync, keyed by our identity.</summary>
    public DbSet<ItemEntity> Items => Set<ItemEntity>();

    /// <summary>Observations of items over time, local and remote.</summary>
    public DbSet<ItemSnapshotEntity> ItemSnapshots => Set<ItemSnapshotEntity>();

    /// <summary>Per-job provider bookmarks, stored in the <c>ProviderState</c> table.</summary>
    public DbSet<ProviderStateEntity> ProviderStates => Set<ProviderStateEntity>();

    /// <summary>Saves inside a <c>persistence.save</c> span; see <see cref="PersistenceTelemetry"/>.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.DetectChanges();

        return PersistenceTelemetry.Record(PendingEntries(), () => base.SaveChanges(acceptAllChangesOnSuccess));
    }

    /// <summary>Saves inside a <c>persistence.save</c> span; see <see cref="PersistenceTelemetry"/>.</summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();

        return PersistenceTelemetry.RecordAsync(PendingEntries(), token => base.SaveChangesAsync(acceptAllChangesOnSuccess, token), cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>(account =>
        {
            account.ToTable("Accounts").HasKey(entity => entity.Id);
            account.HasIndex(entity => new { entity.ProviderType, entity.AccountIdentifier }).IsUnique();
        });

        modelBuilder.Entity<SyncJobEntity>(job =>
        {
            job.ToTable("SyncJobs").HasKey(entity => entity.Id);
            job.HasOne<AccountEntity>().WithMany().HasForeignKey(entity => entity.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ItemEntity>(item =>
        {
            item.ToTable("Items").HasKey(entity => entity.Id);
            item.Property(entity => entity.ItemType).HasConversion<string>();
            item.HasOne<SyncJobEntity>().WithMany().HasForeignKey(entity => entity.SyncJobId).OnDelete(DeleteBehavior.Cascade);
            item.HasOne<ItemEntity>().WithMany().HasForeignKey(entity => entity.ParentItemId).OnDelete(DeleteBehavior.Restrict);
            item.HasIndex(entity => new { entity.SyncJobId, entity.RemoteItemId });
        });

        modelBuilder.Entity<ItemSnapshotEntity>(snapshot =>
        {
            snapshot.ToTable("ItemSnapshots").HasKey(entity => entity.Id);
            snapshot.Property(entity => entity.SnapshotSource).HasConversion<string>();
            snapshot.HasOne<ItemEntity>().WithMany().HasForeignKey(entity => entity.ItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProviderStateEntity>(state =>
        {
            state.ToTable("ProviderState").HasKey(entity => entity.SyncJobId);
            state.HasOne<SyncJobEntity>().WithOne().HasForeignKey<ProviderStateEntity>(entity => entity.SyncJobId).OnDelete(DeleteBehavior.Cascade);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            foreach (var property in entityType.ClrType.GetProperties())
            {
                if (StronglyTypedIdConverters.TryGet(property.PropertyType, out var converter)) modelBuilder.Entity(entityType.ClrType).Property(property.Name).HasConversion(converter);
            }
        }
    }

    private int PendingEntries() => ChangeTracker.Entries().Count(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
}
