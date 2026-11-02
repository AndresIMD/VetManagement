using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConsumptionTakesStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MovesStock",
                table: "ConsumptionSheets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StockPending",
                table: "ConsumptionSheets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ConsumptionStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SheetId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Units = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumptionStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumptionStock_ConsumptionSheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "ConsumptionSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionStock_SheetId_ItemId",
                table: "ConsumptionStock",
                columns: new[] { "SheetId", "ItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumptionStock");

            migrationBuilder.DropColumn(
                name: "MovesStock",
                table: "ConsumptionSheets");

            migrationBuilder.DropColumn(
                name: "StockPending",
                table: "ConsumptionSheets");
        }
    }
}
