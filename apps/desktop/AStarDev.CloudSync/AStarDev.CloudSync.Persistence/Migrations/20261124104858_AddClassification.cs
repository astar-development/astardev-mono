using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AStarDev.CloudSync.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddClassification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Categories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Categories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Terms",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Text = table.Column<string>(type: "TEXT", nullable: false),
                CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                Source = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Terms", x => x.Id);
                table.ForeignKey(
                    name: "FK_Terms_Categories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "Categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FileLinks",
            columns: table => new
            {
                ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                TermId = table.Column<Guid>(type: "TEXT", nullable: false),
                Origin = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FileLinks", x => new { x.ItemId, x.TermId });
                table.ForeignKey(
                    name: "FK_FileLinks_Items_ItemId",
                    column: x => x.ItemId,
                    principalTable: "Items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_FileLinks_Terms_TermId",
                    column: x => x.TermId,
                    principalTable: "Terms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Categories_Name",
            table: "Categories",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FileLinks_TermId_ItemId",
            table: "FileLinks",
            columns: new[] { "TermId", "ItemId" });

        migrationBuilder.CreateIndex(
            name: "IX_Terms_CategoryId_Text",
            table: "Terms",
            columns: new[] { "CategoryId", "Text" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FileLinks");

        migrationBuilder.DropTable(
            name: "Terms");

        migrationBuilder.DropTable(
            name: "Categories");
    }
}
