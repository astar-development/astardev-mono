using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddCrawlCheckpoints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CrawlCheckpoints",
            columns: table => new
            {
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                RemoteRootId = table.Column<string>(type: "TEXT", nullable: false),
                ResumeToken = table.Column<string>(type: "TEXT", nullable: true),
                ItemsIndexed = table.Column<long>(type: "INTEGER", nullable: false),
                Completed = table.Column<bool>(type: "INTEGER", nullable: false),
                DeltaToken = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CrawlCheckpoints", x => new { x.SyncJobId, x.RemoteRootId });
                table.ForeignKey(
                    name: "FK_CrawlCheckpoints_SyncScopes_SyncJobId_RemoteRootId",
                    columns: x => new { x.SyncJobId, x.RemoteRootId },
                    principalTable: "SyncScopes",
                    principalColumns: ["SyncJobId", "RemoteRootId"],
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CrawlCheckpoints");
    }
}
