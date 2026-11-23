using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The EF Core view of <c>telemetry.db</c>, a separate file from the sync database so telemetry volume and failures never touch sync state.</summary>
internal sealed class TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : DbContext(options)
{
    /// <summary>Logs, spans and metric samples.</summary>
    public DbSet<TelemetryEntryEntity> Entries => Set<TelemetryEntryEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<TelemetryEntryEntity>(entry =>
        {
            entry.ToTable("TelemetryEntries").HasKey(entity => entity.Id);
            entry.Property(entity => entity.Source).IsRequired();
            entry.Property(entity => entity.Text).IsRequired();
            entry.HasIndex(entity => new { entity.Kind, entity.TimestampTicks });
            entry.HasIndex(entity => entity.TimestampTicks);
        });
}
