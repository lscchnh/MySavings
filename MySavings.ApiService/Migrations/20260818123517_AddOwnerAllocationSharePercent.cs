using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySavings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerAllocationSharePercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AllocationSharePercent",
                table: "Owner",
                type: "TEXT",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.5m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllocationSharePercent",
                table: "Owner");
        }
    }
}
