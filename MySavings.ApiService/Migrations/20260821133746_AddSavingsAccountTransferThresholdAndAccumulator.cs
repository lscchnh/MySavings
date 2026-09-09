using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySavings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsAccountTransferThresholdAndAccumulator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AccumulatorBefore",
                table: "SavingsAllocations",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferableAmount",
                table: "SavingsAllocations",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferAccumulator",
                table: "SavingsAccounts",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferThreshold",
                table: "SavingsAccounts",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Every existing account has TransferThreshold=0 (no minimum), so the transferable
            // amount for already-recorded months is simply the calculated amount, unaltered.
            migrationBuilder.Sql("""
                UPDATE SavingsAllocations
                SET TransferableAmount = Amount;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccumulatorBefore",
                table: "SavingsAllocations");

            migrationBuilder.DropColumn(
                name: "TransferableAmount",
                table: "SavingsAllocations");

            migrationBuilder.DropColumn(
                name: "TransferAccumulator",
                table: "SavingsAccounts");

            migrationBuilder.DropColumn(
                name: "TransferThreshold",
                table: "SavingsAccounts");
        }
    }
}
