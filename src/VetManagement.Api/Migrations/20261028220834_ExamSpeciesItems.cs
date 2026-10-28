using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExamSpeciesItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing exams, and any an earlier version of the API saves after going back to it, start with no product of
            // their own per species.
            migrationBuilder.AddColumn<string>(
                name: "SpeciesItems",
                table: "Exams",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpeciesItems",
                table: "Exams");
        }
    }
}
