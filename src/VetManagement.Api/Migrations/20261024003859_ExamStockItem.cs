using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExamStockItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemId",
                table: "Exams",
                type: "int",
                nullable: true);

            // Existing exams use one unit each once a product is linked to them.
            migrationBuilder.AddColumn<int>(
                name: "ItemQuantity",
                table: "Exams",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockDeductedAtUtc",
                table: "ExamReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockDeductedBy",
                table: "ExamReports",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockItemId",
                table: "ExamReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockItemName",
                table: "ExamReports",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockQuantity",
                table: "ExamReports",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ItemQuantity",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "StockDeductedAtUtc",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "StockDeductedBy",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "StockItemId",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "StockItemName",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "StockQuantity",
                table: "ExamReports");
        }
    }
}
