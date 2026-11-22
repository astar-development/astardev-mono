using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddKnownSnapshotsAndConflicts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Conflicts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                Type = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conflicts", x => x.Id);
                table.ForeignKey(
                    name: "FK_Conflicts_SyncJobs_SyncJobId",
                    column: x => x.SyncJobId,
                    principalTable: "SyncJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "KnownSnapshots",
            columns: table => new
            {
                ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                Path = table.Column<string>(type: "TEXT", nullable: false),
                Size = table.Column<long>(type: "INTEGER", nullable: false),
                ModifiedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                ETag = table.Column<string>(type: "TEXT", nullable: true),
                Hash = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KnownSnapshots", x => x.ItemId);
                table.ForeignKey(
                    name: "FK_KnownSnapshots_SyncJobs_SyncJobId",
                    column: x => x.SyncJobId,
                    principalTable: "SyncJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StagedKnownSnapshots",
            columns: table => new
            {
                OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                Remove = table.Column<bool>(type: "INTEGER", nullable: false),
                Path = table.Column<string>(type: "TEXT", nullable: false),
                Size = table.Column<long>(type: "INTEGER", nullable: false),
                ModifiedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                ETag = table.Column<string>(type: "TEXT", nullable: true),
                Hash = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StagedKnownSnapshots", x => x.OperationId);
                table.ForeignKey(
                    name: "FK_StagedKnownSnapshots_SyncJobs_SyncJobId",
                    column: x => x.SyncJobId,
                    principalTable: "SyncJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Conflicts_ItemId_Type_Status",
            table: "Conflicts",
            columns: new[] { "ItemId", "Type", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Conflicts_SyncJobId_Status_CreatedUtc",
            table: "Conflicts",
            columns: new[] { "SyncJobId", "Status", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_KnownSnapshots_SyncJobId",
            table: "KnownSnapshots",
            column: "SyncJobId");

        migrationBuilder.CreateIndex(
            name: "IX_StagedKnownSnapshots_SyncJobId",
            table: "StagedKnownSnapshots",
            column: "SyncJobId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Conflicts");

        migrationBuilder.DropTable(
            name: "KnownSnapshots");

        migrationBuilder.DropTable(
            name: "StagedKnownSnapshots");
    }
}
