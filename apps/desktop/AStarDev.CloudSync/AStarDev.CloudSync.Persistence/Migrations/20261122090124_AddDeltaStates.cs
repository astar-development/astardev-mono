using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddDeltaStates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeltaStates",
            columns: table => new
            {
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                RemoteRootId = table.Column<string>(type: "TEXT", nullable: false),
                DeltaToken = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeltaStates", x => new { x.SyncJobId, x.RemoteRootId });
                table.ForeignKey(
                    name: "FK_DeltaStates_SyncScopes_SyncJobId_RemoteRootId",
                    columns: x => new { x.SyncJobId, x.RemoteRootId },
                    principalTable: "SyncScopes",
                    principalColumns: ["SyncJobId", "RemoteRootId"],
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql("INSERT INTO DeltaStates (SyncJobId, RemoteRootId, DeltaToken, UpdatedUtc) SELECT SyncJobId, RemoteRootId, DeltaToken, UpdatedUtc FROM CrawlCheckpoints WHERE DeltaToken IS NOT NULL");

        migrationBuilder.DropColumn(
            name: "DeltaToken",
            table: "CrawlCheckpoints");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DeltaToken",
            table: "CrawlCheckpoints",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql("UPDATE CrawlCheckpoints SET DeltaToken = (SELECT DeltaToken FROM DeltaStates WHERE DeltaStates.SyncJobId = CrawlCheckpoints.SyncJobId AND DeltaStates.RemoteRootId = CrawlCheckpoints.RemoteRootId)");

        migrationBuilder.DropTable(
            name: "DeltaStates");
    }
}
