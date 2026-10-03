using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Anamnesis",
                table: "MedicalVisits",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppointmentId",
                table: "MedicalVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Diagnosis",
                table: "MedicalVisits",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Examination",
                table: "MedicalVisits",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "MedicalVisits",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureC",
                table: "MedicalVisits",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Treatment",
                table: "MedicalVisits",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "MedicalVisits",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PreventiveDoses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PetId = table.Column<int>(type: "int", nullable: false),
                    ProtocolCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppliedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    NextDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    VisitId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AppliedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReminderSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveDoses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalVisits_PatientId",
                table: "MedicalVisits",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveDoses_NextDueOn",
                table: "PreventiveDoses",
                column: "NextDueOn");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveDoses_PetId",
                table: "PreventiveDoses",
                column: "PetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreventiveDoses");

            migrationBuilder.DropIndex(
                name: "IX_MedicalVisits_PatientId",
                table: "MedicalVisits");

            migrationBuilder.DropColumn(
                name: "Anamnesis",
                table: "MedicalVisits");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "MedicalVisits");

            migrationBuilder.DropColumn(
                name: "Diagnosis",
                table: "MedicalVisits");

            migrationBuilder.DropColumn(
                name: "Examination",
                table: "MedicalVisits");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "MedicalVisits");

            migrationBuilder.DropColumn(
                name: "TemperatureC",
                table: "MedicalVisits");

            migrationBuilder.DropColumn(
                name: "Treatment",
                table: "MedicalVisits");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "MedicalVisits");
        }
    }
}
