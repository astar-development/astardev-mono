using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <summary>Rewrites every timestamp from EF's <c>DateTimeOffsetToBinaryConverter</c> form (local ticks / 1000 shifted left 11 bits, with the offset in minutes as a signed 11-bit low field) to full UTC ticks. The schema does not change: the columns stay INTEGER.</summary>
internal sealed partial class StoreFullTicks : Migration
{
    private static readonly (string Table, string Column)[] Timestamps =
    [
        ("Accounts", "CreatedUtc"),
        ("SyncJobs", "CreatedUtc"),
        ("SyncJobs", "UpdatedUtc"),
        ("CrawlCheckpoints", "UpdatedUtc"),
        ("DeltaStates", "UpdatedUtc"),
        ("Operations", "NextAttemptUtc"),
        ("Operations", "CreatedUtc"),
        ("KnownSnapshots", "ModifiedUtc"),
        ("StagedKnownSnapshots", "ModifiedUtc"),
        ("Conflicts", "CreatedUtc"),
        ("EventJournal", "OccurredUtc"),
        ("Items", "CreatedUtc"),
        ("Items", "UpdatedUtc"),
        ("ItemSnapshots", "ModifiedUtc"),
        ("ItemSnapshots", "ObservedUtc"),
        ("ProviderState", "LastSyncUtc"),
        ("ProviderState", "LastScanUtc")
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, column) in Timestamps)
            migrationBuilder.Sql($"UPDATE \"{table}\" SET \"{column}\" = (\"{column}\" >> 11) * 1000 - (CASE WHEN (\"{column}\" & 1024) = 0 THEN \"{column}\" & 2047 ELSE (\"{column}\" & 2047) - 2048 END) * 600000000;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, column) in Timestamps)
            migrationBuilder.Sql($"UPDATE \"{table}\" SET \"{column}\" = (\"{column}\" / 1000) << 11;");
    }
}
