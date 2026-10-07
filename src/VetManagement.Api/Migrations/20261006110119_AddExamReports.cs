using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExamReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PetId = table.Column<int>(type: "int", nullable: false),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReferringVet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Interpretation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Sections_PatientData = table.Column<bool>(type: "bit", nullable: false),
                    Sections_Method = table.Column<bool>(type: "bit", nullable: false),
                    Sections_Interpretation = table.Column<bool>(type: "bit", nullable: false),
                    Sections_ReportedBy = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Results = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicImages_Kind",
                table: "ClinicImages",
                column: "Kind",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamReports_PetId",
                table: "ExamReports",
                column: "PetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicImages");

            migrationBuilder.DropTable(
                name: "ExamReports");
        }
    }
}
