using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class LabMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabMachines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabMachines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabMachines_Brand_Model",
                table: "LabMachines",
                columns: new[] { "Brand", "Model" },
                unique: true);

            // Start the list with the brands and models the catalog exams already use.
            migrationBuilder.Sql("""
                INSERT INTO LabMachines (Brand, Model)
                SELECT MIN(LEFT(LTRIM(RTRIM(Brand)), 100)), MIN(LEFT(LTRIM(RTRIM(Machine)), 100))
                FROM Exams
                WHERE LTRIM(RTRIM(Brand)) <> '' AND LTRIM(RTRIM(Machine)) <> ''
                GROUP BY LEFT(LTRIM(RTRIM(Brand)), 100), LEFT(LTRIM(RTRIM(Machine)), 100)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabMachines");
        }
    }
}
