using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class WorkdayTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkdayTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkdayTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkdayTargets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "WorkdayTargets",
                columns: new[] { "Id", "DayOfWeek", "Hours", "UserId" },
                values: new object[,]
                {
                    { 1, 1, 8m, null },
                    { 2, 2, 8m, null },
                    { 3, 3, 8m, null },
                    { 4, 4, 8m, null },
                    { 5, 5, 8m, null },
                    { 6, 6, 0m, null },
                    { 7, 0, 0m, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkdayTargets_DayOfWeek_Global",
                table: "WorkdayTargets",
                column: "DayOfWeek",
                unique: true,
                filter: "\"UserId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkdayTargets_UserId_DayOfWeek",
                table: "WorkdayTargets",
                columns: new[] { "UserId", "DayOfWeek" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkdayTargets");
        }
    }
}
