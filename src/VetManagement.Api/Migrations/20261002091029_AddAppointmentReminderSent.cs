using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentReminderSent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAtUtc",
                table: "Appointments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSentAtUtc",
                table: "Appointments");
        }
    }
}
