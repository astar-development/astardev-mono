using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.TelemetryMigrations;

/// <inheritdoc />
internal sealed partial class AddTelemetry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TelemetryEntries",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                TimestampTicks = table.Column<long>(type: "INTEGER", nullable: false),
                Level = table.Column<int>(type: "INTEGER", nullable: false),
                Source = table.Column<string>(type: "TEXT", nullable: false),
                Text = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TelemetryEntries", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TelemetryEntries_Kind_TimestampTicks",
            table: "TelemetryEntries",
            columns: new[] { "Kind", "TimestampTicks" });

        migrationBuilder.CreateIndex(
            name: "IX_TelemetryEntries_TimestampTicks",
            table: "TelemetryEntries",
            column: "TimestampTicks");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TelemetryEntries");
    }
}
