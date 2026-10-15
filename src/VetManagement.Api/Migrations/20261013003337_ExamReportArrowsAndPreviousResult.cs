using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExamReportArrowsAndPreviousResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PreviousReceivedOn",
                table: "ExamReports",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Sections_DirectionArrows",
                table: "ExamReports",
                type: "bit",
                nullable: false,
                defaultValue: true); // reports issued before the setting existed printed the arrows

            migrationBuilder.AddColumn<bool>(
                name: "Sections_PreviousResult",
                table: "ExamReports",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousReceivedOn",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "Sections_DirectionArrows",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "Sections_PreviousResult",
                table: "ExamReports");
        }
    }
}
