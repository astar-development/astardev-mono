using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderType = table.Column<string>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                AccountIdentifier = table.Column<string>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Accounts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SyncJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                LocalRootPath = table.Column<string>(type: "TEXT", nullable: false),
                RemoteRootId = table.Column<string>(type: "TEXT", nullable: false),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SyncJobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_SyncJobs_Accounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                ItemType = table.Column<string>(type: "TEXT", nullable: false),
                ParentItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                RemoteItemId = table.Column<string>(type: "TEXT", nullable: false),
                LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Items", x => x.Id);
                table.ForeignKey(
                    name: "FK_Items_Items_ParentItemId",
                    column: x => x.ParentItemId,
                    principalTable: "Items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Items_SyncJobs_SyncJobId",
                    column: x => x.SyncJobId,
                    principalTable: "SyncJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProviderState",
            columns: table => new
            {
                SyncJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                DeltaToken = table.Column<string>(type: "TEXT", nullable: true),
                LastSyncUtc = table.Column<long>(type: "INTEGER", nullable: true),
                LastScanUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderState", x => x.SyncJobId);
                table.ForeignKey(
                    name: "FK_ProviderState_SyncJobs_SyncJobId",
                    column: x => x.SyncJobId,
                    principalTable: "SyncJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ItemSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                Size = table.Column<long>(type: "INTEGER", nullable: false),
                ModifiedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                Hash = table.Column<string>(type: "TEXT", nullable: true),
                ETag = table.Column<string>(type: "TEXT", nullable: true),
                CTag = table.Column<string>(type: "TEXT", nullable: true),
                ObservedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                SnapshotSource = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ItemSnapshots", x => x.Id);
                table.ForeignKey(
                    name: "FK_ItemSnapshots_Items_ItemId",
                    column: x => x.ItemId,
                    principalTable: "Items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Accounts_ProviderType_AccountIdentifier",
            table: "Accounts",
            columns: new[] { "ProviderType", "AccountIdentifier" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Items_ParentItemId",
            table: "Items",
            column: "ParentItemId");

        migrationBuilder.CreateIndex(
            name: "IX_Items_SyncJobId_RemoteItemId",
            table: "Items",
            columns: new[] { "SyncJobId", "RemoteItemId" });

        migrationBuilder.CreateIndex(
            name: "IX_ItemSnapshots_ItemId",
            table: "ItemSnapshots",
            column: "ItemId");

        migrationBuilder.CreateIndex(
            name: "IX_SyncJobs_AccountId",
            table: "SyncJobs",
            column: "AccountId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ItemSnapshots");

        migrationBuilder.DropTable(
            name: "ProviderState");

        migrationBuilder.DropTable(
            name: "Items");

        migrationBuilder.DropTable(
            name: "SyncJobs");

        migrationBuilder.DropTable(
            name: "Accounts");
    }
}
