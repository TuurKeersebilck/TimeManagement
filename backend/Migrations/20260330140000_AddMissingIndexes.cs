using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_Date",
                table: "TimeLogs",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_UserId_Date",
                table: "TimeLogs",
                columns: ["UserId", "Date"]);

            migrationBuilder.CreateIndex(
                name: "IX_VacationDays_UserId_Date",
                table: "VacationDays",
                columns: ["UserId", "Date"]);

            migrationBuilder.CreateIndex(
                name: "IX_VacationDays_VacationTypeId",
                table: "VacationDays",
                column: "VacationTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_Date",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_UserId_Date",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_VacationDays_UserId_Date",
                table: "VacationDays");

            migrationBuilder.DropIndex(
                name: "IX_VacationDays_VacationTypeId",
                table: "VacationDays");
        }
    }
}
