using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySavings.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class RefactorOwnerAndTransferGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create Owner table (was PersonSettings)
            migrationBuilder.CreateTable(
                name: "Owner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Owner", x => x.Id);
                });

            // 2. Migrate PersonSettings data → Owner (DisplayName → Name, preserve Id ordering)
            migrationBuilder.Sql("""
                INSERT INTO Owner (Id, Name, LastModified)
                SELECT Id, DisplayName, LastModified FROM PersonSettings
                ORDER BY Id;
                """);

            // 3. Add OwnerId column to SavingsAccounts (nullable)
            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "SavingsAccounts",
                type: "INTEGER",
                nullable: true);

            // 4. Migrate existing Owner string → OwnerId FK
            //    Person1 had Id=1 (first row in PersonSettings by PersonKey='Person1')
            //    Person2 had Id=2 (second row)
            migrationBuilder.Sql("""
                UPDATE SavingsAccounts
                SET OwnerId = (SELECT Id FROM Owner ORDER BY Id LIMIT 1 OFFSET 0)
                WHERE Owner = 'Person1';
                """);

            migrationBuilder.Sql("""
                UPDATE SavingsAccounts
                SET OwnerId = (SELECT Id FROM Owner ORDER BY Id LIMIT 1 OFFSET 1)
                WHERE Owner = 'Person2';
                """);

            // 5. Drop PersonSettings (data migrated above)
            migrationBuilder.DropTable(
                name: "PersonSettings");

            // 6. Drop old Owner string column from SavingsAccounts
            migrationBuilder.DropColumn(
                name: "Owner",
                table: "SavingsAccounts");

            // 7. Rename TransferGroups.Label → Name
            migrationBuilder.RenameColumn(
                name: "Label",
                table: "TransferGroups",
                newName: "Name");

            // 8. Create many-to-many join table for TransferGroup ↔ SavingsAccount
            migrationBuilder.CreateTable(
                name: "TransferGroupSavingsAccount",
                columns: table => new
                {
                    SavingsAccountsId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransferGroupsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferGroupSavingsAccount", x => new { x.SavingsAccountsId, x.TransferGroupsId });
                    table.ForeignKey(
                        name: "FK_TransferGroupSavingsAccount_SavingsAccounts_SavingsAccountsId",
                        column: x => x.SavingsAccountsId,
                        principalTable: "SavingsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransferGroupSavingsAccount_TransferGroups_TransferGroupsId",
                        column: x => x.TransferGroupsId,
                        principalTable: "TransferGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 9. Migrate TransferGroupItems → join table (match by account name)
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO TransferGroupSavingsAccount (SavingsAccountsId, TransferGroupsId)
                SELECT sa.Id, tgi.TransferGroupId
                FROM TransferGroupItems tgi
                INNER JOIN SavingsAccounts sa ON sa.Name = tgi.AccountName;
                """);

            // 10. Drop TransferGroupItems (data migrated above)
            migrationBuilder.DropTable(
                name: "TransferGroupItems");

            // 11. Add indexes and FK
            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_OwnerId",
                table: "SavingsAccounts",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferGroupSavingsAccount_TransferGroupsId",
                table: "TransferGroupSavingsAccount",
                column: "TransferGroupsId");

            migrationBuilder.AddForeignKey(
                name: "FK_SavingsAccounts_Owner_OwnerId",
                table: "SavingsAccounts",
                column: "OwnerId",
                principalTable: "Owner",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavingsAccounts_Owner_OwnerId",
                table: "SavingsAccounts");

            migrationBuilder.DropTable(
                name: "TransferGroupSavingsAccount");

            migrationBuilder.DropIndex(
                name: "IX_SavingsAccounts_OwnerId",
                table: "SavingsAccounts");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "SavingsAccounts");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "TransferGroups",
                newName: "Label");

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "SavingsAccounts",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PersonSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PersonKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonSettings", x => x.Id);
                });

            // Restore PersonSettings from Owner (best effort - PersonKey is lost)
            migrationBuilder.Sql("""
                INSERT INTO PersonSettings (Id, PersonKey, DisplayName, LastModified)
                SELECT Id, CASE Id WHEN 1 THEN 'Person1' WHEN 2 THEN 'Person2' ELSE 'Person' || Id END, Name, LastModified
                FROM Owner;
                """);

            migrationBuilder.DropTable(
                name: "Owner");

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
                name: "IX_PersonSettings_PersonKey",
                table: "PersonSettings",
                column: "PersonKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferGroupItems_TransferGroupId",
                table: "TransferGroupItems",
                column: "TransferGroupId");
        }
    }
}
