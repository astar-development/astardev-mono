using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddEventJournal : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EventJournal",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurredUtc = table.Column<long>(type: "INTEGER", nullable: false),
                EventType = table.Column<string>(type: "TEXT", nullable: false),
                AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventJournal", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EventJournal_AggregateId_OccurredUtc",
            table: "EventJournal",
            columns: new[] { "AggregateId", "OccurredUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EventJournal");
    }
}
