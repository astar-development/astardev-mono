using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddClassificationState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClassificationState",
            columns: table => new
            {
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClassificationState", x => x.Name);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ClassificationState");
    }
}
