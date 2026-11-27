using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddAccountConcurrency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Operations_SyncJobId",
            table: "Operations");

        migrationBuilder.AddColumn<int>(
            name: "MaxConcurrentOperations",
            table: "Accounts",
            type: "INTEGER",
            nullable: false,
            defaultValue: 4);

        migrationBuilder.CreateIndex(
            name: "IX_Operations_SyncJobId_Status_Priority_CreatedUtc",
            table: "Operations",
            columns: new[] { "SyncJobId", "Status", "Priority", "CreatedUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Operations_SyncJobId_Status_Priority_CreatedUtc",
            table: "Operations");

        migrationBuilder.DropColumn(
            name: "MaxConcurrentOperations",
            table: "Accounts");

        migrationBuilder.CreateIndex(
            name: "IX_Operations_SyncJobId",
            table: "Operations",
            column: "SyncJobId");
    }
}
