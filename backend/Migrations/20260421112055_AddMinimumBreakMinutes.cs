using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMinimumBreakMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumBreakMinutes",
                table: "EmployeeTargets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumBreakMinutes",
                table: "AppConfigurations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumBreakMinutes",
                table: "EmployeeTargets");

            migrationBuilder.DropColumn(
                name: "MinimumBreakMinutes",
                table: "AppConfigurations");
        }
    }
}
