using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarFeedToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CalendarTokenExpiresAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalendarTokenHash",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CalendarTokenHash",
                table: "AspNetUsers",
                column: "CalendarTokenHash",
                unique: true,
                filter: "\"CalendarTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CalendarTokenHash",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CalendarTokenExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CalendarTokenHash",
                table: "AspNetUsers");
        }
    }
}
