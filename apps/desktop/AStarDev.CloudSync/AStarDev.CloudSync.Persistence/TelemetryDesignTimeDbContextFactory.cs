using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Lets <c>dotnet ef migrations add --context TelemetryDbContext</c> build the context without running the daemon. Never used at runtime.</summary>
internal sealed class TelemetryDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TelemetryDbContext>
{
    /// <inheritdoc/>
    public TelemetryDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<TelemetryDbContext>().UseSqlite("Data Source=design-time-telemetry.db").Options);
}
