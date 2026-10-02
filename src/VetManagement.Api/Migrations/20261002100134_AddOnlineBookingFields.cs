using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineBookingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepositStatus",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaidAmount",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentProvider",
                table: "Appointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentToken",
                table: "Appointments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicToken",
                table: "Appointments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PaymentToken",
                table: "Appointments",
                column: "PaymentToken");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PublicToken",
                table: "Appointments",
                column: "PublicToken",
                unique: true,
                filter: "[PublicToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_PaymentToken",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PublicToken",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DepositStatus",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaymentProvider",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaymentToken",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PublicToken",
                table: "Appointments");
        }
    }
}
