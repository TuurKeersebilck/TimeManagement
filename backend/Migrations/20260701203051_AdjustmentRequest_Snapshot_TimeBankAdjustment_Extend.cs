using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AdjustmentRequest_Snapshot_TimeBankAdjustment_Extend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedBreakEnd",
                table: "TimeAdjustmentRequests");

            migrationBuilder.DropColumn(
                name: "RequestedBreakStart",
                table: "TimeAdjustmentRequests");

            migrationBuilder.DropColumn(
                name: "RequestedClockIn",
                table: "TimeAdjustmentRequests");

            migrationBuilder.DropColumn(
                name: "RequestedClockOut",
                table: "TimeAdjustmentRequests");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "TimeBankAdjustments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "TimeBankAdjustments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "TimeBankAdjustments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesiredDaySnapshot",
                table: "TimeAdjustmentRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TimeBankAdjustments_CreatedByUserId",
                table: "TimeBankAdjustments",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeBankAdjustments_AspNetUsers_CreatedByUserId",
                table: "TimeBankAdjustments",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeBankAdjustments_AspNetUsers_CreatedByUserId",
                table: "TimeBankAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_TimeBankAdjustments_CreatedByUserId",
                table: "TimeBankAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TimeBankAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "TimeBankAdjustments");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "TimeBankAdjustments");

            migrationBuilder.DropColumn(
                name: "DesiredDaySnapshot",
                table: "TimeAdjustmentRequests");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedBreakEnd",
                table: "TimeAdjustmentRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedBreakStart",
                table: "TimeAdjustmentRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedClockIn",
                table: "TimeAdjustmentRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedClockOut",
                table: "TimeAdjustmentRequests",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
