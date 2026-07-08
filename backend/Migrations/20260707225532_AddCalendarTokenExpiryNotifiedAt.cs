using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarTokenExpiryNotifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CalendarTokenExpiryNotifiedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalendarTokenExpiryNotifiedAt",
                table: "AspNetUsers");
        }
    }
}
