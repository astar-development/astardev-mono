using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class KeepLatestItemSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ItemSnapshots_ItemId",
            table: "ItemSnapshots");

        migrationBuilder.Sql("DELETE FROM ItemSnapshots WHERE rowid IN (SELECT rowid FROM (SELECT rowid, ROW_NUMBER() OVER (PARTITION BY ItemId, SnapshotSource ORDER BY ObservedUtc DESC, rowid DESC) AS Position FROM ItemSnapshots) WHERE Position > 1);");

        migrationBuilder.CreateIndex(
            name: "IX_ItemSnapshots_ItemId_SnapshotSource",
            table: "ItemSnapshots",
            columns: new[] { "ItemId", "SnapshotSource" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ItemSnapshots_ItemId_SnapshotSource",
            table: "ItemSnapshots");

        migrationBuilder.CreateIndex(
            name: "IX_ItemSnapshots_ItemId",
            table: "ItemSnapshots",
            column: "ItemId");
    }
}
