using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddOperations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Operations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                Type = table.Column<string>(type: "TEXT", nullable: false),
                Priority = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                NextAttemptUtc = table.Column<long>(type: "INTEGER", nullable: false),
                LastError = table.Column<string>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Operations", x => x.Id);
                table.ForeignKey(
                    name: "FK_Operations_SyncJobs_SyncJobId",
                    column: x => x.SyncJobId,
                    principalTable: "SyncJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Operations_ItemId_Status",
            table: "Operations",
            columns: new[] { "ItemId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Operations_Status_Priority_CreatedUtc",
            table: "Operations",
            columns: new[] { "Status", "Priority", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Operations_SyncJobId",
            table: "Operations",
            column: "SyncJobId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(
            name: "Operations");
}
