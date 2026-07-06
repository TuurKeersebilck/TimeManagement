using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class SettlementAllocationAndSourcedAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceSettlementId",
                table: "TimeBankAdjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CarriedForwardHours",
                table: "MonthlySettlements",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidOutHours",
                table: "MonthlySettlements",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeBankAdjustments_SourceSettlementId",
                table: "TimeBankAdjustments",
                column: "SourceSettlementId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeBankAdjustments_MonthlySettlements_SourceSettlementId",
                table: "TimeBankAdjustments",
                column: "SourceSettlementId",
                principalTable: "MonthlySettlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeBankAdjustments_MonthlySettlements_SourceSettlementId",
                table: "TimeBankAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_TimeBankAdjustments_SourceSettlementId",
                table: "TimeBankAdjustments");

            migrationBuilder.DropColumn(
                name: "SourceSettlementId",
                table: "TimeBankAdjustments");

            migrationBuilder.DropColumn(
                name: "CarriedForwardHours",
                table: "MonthlySettlements");

            migrationBuilder.DropColumn(
                name: "PaidOutHours",
                table: "MonthlySettlements");
        }
    }
}
