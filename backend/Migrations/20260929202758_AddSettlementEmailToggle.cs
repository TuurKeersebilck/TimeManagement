using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementEmailToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableSettlementEmails",
                table: "AppConfigurations",
                type: "boolean",
                nullable: false,
                // On for existing installs, like a fresh AppConfiguration row.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableSettlementEmails",
                table: "AppConfigurations");
        }
    }
}
