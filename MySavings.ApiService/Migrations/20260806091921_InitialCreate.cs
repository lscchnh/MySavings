using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySavings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Month = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Person1Salary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Person1Expenses = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Person1Savings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Person1SavingsRatio = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    Person2Salary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Person2Expenses = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Person2Savings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Person2SavingsRatio = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    TotalSalary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalExpenses = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalSavings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalSavingsRatio = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    PreviousMonthPerson1Percent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    PreviousMonthPerson2Percent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingsAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransferGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllocationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SavingsAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationRules_SavingsAccounts_SavingsAccountId",
                        column: x => x.SavingsAccountId,
                        principalTable: "SavingsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavingsAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MonthlyEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SavingsAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    Weight = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    IsTransferred = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsAllocations_MonthlyEntries_MonthlyEntryId",
                        column: x => x.MonthlyEntryId,
                        principalTable: "MonthlyEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavingsAllocations_SavingsAccounts_SavingsAccountId",
                        column: x => x.SavingsAccountId,
                        principalTable: "SavingsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferGroupItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransferGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferGroupItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferGroupItems_TransferGroups_TransferGroupId",
                        column: x => x.TransferGroupId,
                        principalTable: "TransferGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllocationRules_SavingsAccountId",
                table: "AllocationRules",
                column: "SavingsAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyEntries_Month",
                table: "MonthlyEntries",
                column: "Month",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonSettings_PersonKey",
                table: "PersonSettings",
                column: "PersonKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_Name",
                table: "SavingsAccounts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAllocations_MonthlyEntryId_SavingsAccountId",
                table: "SavingsAllocations",
                columns: new[] { "MonthlyEntryId", "SavingsAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAllocations_SavingsAccountId",
                table: "SavingsAllocations",
                column: "SavingsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferGroupItems_TransferGroupId",
                table: "TransferGroupItems",
                column: "TransferGroupId");

            migrationBuilder.InsertData(
                table: "PersonSettings",
                columns: new[] { "Id", "PersonKey", "DisplayName", "LastModified" },
                values: new object[,]
                {
                    { 1, "Person1", "Louis", new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Person2", "Alice", new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllocationRules");

            migrationBuilder.DropTable(
                name: "PersonSettings");

            migrationBuilder.DropTable(
                name: "SavingsAllocations");

            migrationBuilder.DropTable(
                name: "TransferGroupItems");

            migrationBuilder.DropTable(
                name: "MonthlyEntries");

            migrationBuilder.DropTable(
                name: "SavingsAccounts");

            migrationBuilder.DropTable(
                name: "TransferGroups");
        }
    }
}
