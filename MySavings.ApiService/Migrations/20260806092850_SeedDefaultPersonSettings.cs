using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySavings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultPersonSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO PersonSettings (PersonKey, DisplayName, LastModified)
                VALUES ('Person1', 'Louis', '2026-08-06 00:00:00Z');
                """);

            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO PersonSettings (PersonKey, DisplayName, LastModified)
                VALUES ('Person2', 'Alice', '2026-08-06 00:00:00Z');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM PersonSettings
                WHERE PersonKey IN ('Person1', 'Person2');
                """);
        }
    }
}
