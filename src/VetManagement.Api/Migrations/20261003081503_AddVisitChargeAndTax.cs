using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitChargeAndTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VisitId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SkipStock",
                table: "SaleLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TaxExempt",
                table: "SaleLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_VisitId",
                table: "Sales",
                column: "VisitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_VisitId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SkipStock",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "TaxExempt",
                table: "SaleLines");
        }
    }
}
