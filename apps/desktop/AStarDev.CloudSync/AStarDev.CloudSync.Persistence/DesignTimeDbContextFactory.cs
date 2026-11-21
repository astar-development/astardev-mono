using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without running the daemon. Never used at runtime.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CloudSyncDbContext>
{
    /// <inheritdoc/>
    public CloudSyncDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<CloudSyncDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
