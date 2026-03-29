using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBreakStartEndAndDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Break",
                table: "TimeLogs");

            migrationBuilder.AddColumn<string>(
                name: "BreakEnd",
                table: "TimeLogs",
                type: "varchar(8)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BreakStart",
                table: "TimeLogs",
                type: "varchar(8)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "TimeLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreakEnd",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "BreakStart",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "TimeLogs");

            migrationBuilder.AddColumn<string>(
                name: "Break",
                table: "TimeLogs",
                type: "varchar(8)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
