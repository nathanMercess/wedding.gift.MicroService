using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class HardenPaymentsAuthenticationAndContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Available",
                table: "Gifts");

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Payments",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Gifts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Couples",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "MessageArchivedAtUtc",
                table: "Contributions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MessageReadAtUtc",
                table: "Contributions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Contributions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GiftId_Status_ExpiresAt",
                table: "Payments",
                columns: new[] { "GiftId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status_UpdatedAt",
                table: "Payments",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_MessageArchivedAtUtc_MessageReadAtUtc",
                table: "Contributions",
                columns: new[] { "MessageArchivedAtUtc", "MessageReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_Status_PaidAt",
                table: "Contributions",
                columns: new[] { "Status", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAtUtc",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Payments_GiftId_Status_ExpiresAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Status_UpdatedAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_MessageArchivedAtUtc_MessageReadAtUtc",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_Status_PaidAt",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Couples");

            migrationBuilder.DropColumn(
                name: "MessageArchivedAtUtc",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "MessageReadAtUtc",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Contributions");

            migrationBuilder.AddColumn<bool>(
                name: "Available",
                table: "Gifts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("0ac3abdd-0c2d-4234-b72b-b327d8563af7"),
                column: "Available",
                value: true);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("42fdcc72-664b-4d65-95e2-e8f4f906f28b"),
                column: "Available",
                value: true);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                column: "Available",
                value: true);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"),
                column: "Available",
                value: true);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("cbb2ebce-0130-4acc-aebc-054ca72cbfca"),
                column: "Available",
                value: true);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("df173a4e-d8f8-472f-ae72-7b64e3e8f076"),
                column: "Available",
                value: true);
        }
    }
}
