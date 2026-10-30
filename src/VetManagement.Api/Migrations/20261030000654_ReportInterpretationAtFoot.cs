using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReportInterpretationAtFoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Sections_InterpretationAtFoot",
                table: "ExamReports",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sections_InterpretationAtFoot",
                table: "ExamReports");
        }
    }
}
