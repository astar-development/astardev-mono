using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddLocalIdentity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LocalIdentity",
            table: "ItemSnapshots",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LocalIdentity",
            table: "ItemSnapshots");
    }
}
