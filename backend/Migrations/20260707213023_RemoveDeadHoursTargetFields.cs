using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeadHoursTargetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyHours",
                table: "EmployeeTargets");

            migrationBuilder.DropColumn(
                name: "WeeklyHours",
                table: "EmployeeTargets");

            migrationBuilder.DropColumn(
                name: "DefaultDailyHours",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "DefaultWeeklyHours",
                table: "AppConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyHours",
                table: "EmployeeTargets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeeklyHours",
                table: "EmployeeTargets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDailyHours",
                table: "AppConfigurations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultWeeklyHours",
                table: "AppConfigurations",
                type: "numeric",
                nullable: true);
        }
    }
}
