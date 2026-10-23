using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Barcode",
                table: "Items",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Date",
                table: "InventoryMovements",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ExamReports_ReceivedOn",
                table: "ExamReports",
                column: "ReceivedOn");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId_EntityName",
                table: "AuditLogs",
                columns: new[] { "EntityId", "EntityName" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_Date",
                table: "AuditLogs",
                columns: new[] { "EntityName", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_Barcode",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_Date",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_ExamReports_ReceivedOn",
                table: "ExamReports");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityId_EntityName",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityName_Date",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
