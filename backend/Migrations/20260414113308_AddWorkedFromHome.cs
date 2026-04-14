using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkedFromHome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WorkedFromHome",
                table: "ClockEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkedFromHome",
                table: "ClockEvents");
        }
    }
}
