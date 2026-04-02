using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add default target columns to AppConfigurations
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDailyHours",
                table: "AppConfigurations",
                type: "numeric(4,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultWeeklyHours",
                table: "AppConfigurations",
                type: "numeric(4,2)",
                nullable: true);

            // Create EmployeeTargets table
            migrationBuilder.CreateTable(
                name: "EmployeeTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DailyHours = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    WeeklyHours = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeTargets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTargets_UserId",
                table: "EmployeeTargets",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmployeeTargets");

            migrationBuilder.DropColumn(name: "DefaultDailyHours", table: "AppConfigurations");
            migrationBuilder.DropColumn(name: "DefaultWeeklyHours", table: "AppConfigurations");
        }
    }
}
