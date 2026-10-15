using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExamReportTypedPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PetId",
                table: "ExamReports",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Patient_Birthdate",
                table: "ExamReports",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Patient_Breed",
                table: "ExamReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Patient_Name",
                table: "ExamReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Patient_OwnerLastName",
                table: "ExamReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Patient_OwnerName",
                table: "ExamReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Patient_Sex",
                table: "ExamReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Patient_Species",
                table: "ExamReports",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Patient_Birthdate",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "Patient_Breed",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "Patient_Name",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "Patient_OwnerLastName",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "Patient_OwnerName",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "Patient_Sex",
                table: "ExamReports");

            migrationBuilder.DropColumn(
                name: "Patient_Species",
                table: "ExamReports");

            migrationBuilder.AlterColumn<int>(
                name: "PetId",
                table: "ExamReports",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
