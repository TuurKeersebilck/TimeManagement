using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseMaxSessionHoursDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "MaxSessionHours",
                table: "AppConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 13m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldDefaultValue: 10m);

            // Update any existing row still at the old default so live config is corrected immediately.
            migrationBuilder.Sql("UPDATE \"AppConfigurations\" SET \"MaxSessionHours\" = 13 WHERE \"MaxSessionHours\" = 10;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "MaxSessionHours",
                table: "AppConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 10m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldDefaultValue: 13m);
        }
    }
}
