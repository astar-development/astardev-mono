using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddSyncScopes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BootstrapMode",
            table: "SyncJobs",
            type: "TEXT",
            nullable: false,
            defaultValue: "Merge");

        migrationBuilder.CreateTable(
            name: "SyncScopes",
            columns: table => new
            {
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                RemoteRootId = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SyncScopes", x => new { x.SyncJobId, x.RemoteRootId });
                table.ForeignKey(
                    name: "FK_SyncScopes_SyncJobs_SyncJobId",
                    column: x => x.SyncJobId,
                    principalTable: "SyncJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SyncScopes");

        migrationBuilder.DropColumn(
            name: "BootstrapMode",
            table: "SyncJobs");
    }
}
