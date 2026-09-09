using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySavings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsAccountLiquidityLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LiquidityLevel",
                table: "SavingsAccounts",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Liquide");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LiquidityLevel",
                table: "SavingsAccounts");
        }
    }
}
