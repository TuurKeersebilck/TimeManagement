using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class AdjustmentRequestDateTimeOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // interval cannot be cast to timestamptz automatically in PostgreSQL.
            // Drop the old interval columns and recreate as timestamptz (nullable).
            // Any existing pending requests will have null time values after this
            // migration; the old values were stored incorrectly (no timezone info)
            // and cannot be meaningfully converted.
            migrationBuilder.DropColumn(name: "RequestedClockIn",    table: "TimeAdjustmentRequests");
            migrationBuilder.DropColumn(name: "RequestedBreakStart", table: "TimeAdjustmentRequests");
            migrationBuilder.DropColumn(name: "RequestedBreakEnd",   table: "TimeAdjustmentRequests");
            migrationBuilder.DropColumn(name: "RequestedClockOut",   table: "TimeAdjustmentRequests");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedClockIn",
                table: "TimeAdjustmentRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedBreakStart",
                table: "TimeAdjustmentRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedBreakEnd",
                table: "TimeAdjustmentRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedClockOut",
                table: "TimeAdjustmentRequests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RequestedClockIn",    table: "TimeAdjustmentRequests");
            migrationBuilder.DropColumn(name: "RequestedBreakStart", table: "TimeAdjustmentRequests");
            migrationBuilder.DropColumn(name: "RequestedBreakEnd",   table: "TimeAdjustmentRequests");
            migrationBuilder.DropColumn(name: "RequestedClockOut",   table: "TimeAdjustmentRequests");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "RequestedClockIn",
                table: "TimeAdjustmentRequests",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "RequestedBreakStart",
                table: "TimeAdjustmentRequests",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "RequestedBreakEnd",
                table: "TimeAdjustmentRequests",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "RequestedClockOut",
                table: "TimeAdjustmentRequests",
                type: "interval",
                nullable: true);
        }
    }
}
