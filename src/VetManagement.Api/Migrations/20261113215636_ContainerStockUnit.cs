using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ContainerStockUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StockUnitMl",
                table: "ConsumptionPricings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StockUnitMl",
                table: "ConsumptionEntries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockUnitMl",
                table: "ConsumptionPricings");

            migrationBuilder.DropColumn(
                name: "StockUnitMl",
                table: "ConsumptionEntries");
        }
    }
}
