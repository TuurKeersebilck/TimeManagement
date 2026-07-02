using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class WorkSession_BreakRecord_WorkDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyOvertimeAllowanceHours",
                table: "EmployeeTargets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeeklyOvertimeAllowanceHours",
                table: "EmployeeTargets",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDailyOvertimeAllowanceHours",
                table: "AppConfigurations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultWeeklyOvertimeAllowanceHours",
                table: "AppConfigurations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxSessionHours",
                table: "AppConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.CreateTable(
                name: "WorkDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkedFromHome = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkDays_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ClockIn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClockInServerStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClockOut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClockOutServerStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BreakRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkSessionId = table.Column<int>(type: "integer", nullable: false),
                    BreakStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BreakStartServerStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BreakEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BreakEndServerStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreakRecords_WorkSessions_WorkSessionId",
                        column: x => x.WorkSessionId,
                        principalTable: "WorkSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BreakRecords_WorkSessionId_Open",
                table: "BreakRecords",
                column: "WorkSessionId",
                unique: true,
                filter: "\"BreakEnd\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkDays_UserId_Date",
                table: "WorkDays",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_UserId_Date",
                table: "WorkSessions",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_UserId_Open",
                table: "WorkSessions",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BreakRecords");

            migrationBuilder.DropTable(
                name: "WorkDays");

            migrationBuilder.DropTable(
                name: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "DailyOvertimeAllowanceHours",
                table: "EmployeeTargets");

            migrationBuilder.DropColumn(
                name: "WeeklyOvertimeAllowanceHours",
                table: "EmployeeTargets");

            migrationBuilder.DropColumn(
                name: "DefaultDailyOvertimeAllowanceHours",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "DefaultWeeklyOvertimeAllowanceHours",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "MaxSessionHours",
                table: "AppConfigurations");
        }
    }
}
