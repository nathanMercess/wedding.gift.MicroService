using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrativeOperationsAndOrderLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoupleId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoupleId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Gifts",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AddColumn<Guid>(
                name: "CoupleId",
                table: "Gifts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CoupleId",
                table: "Contributions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Contributions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "Contributions",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderId",
                table: "Contributions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Contributions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CoupleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.Sql("UPDATE [Users] SET [CoupleId] = '11111111-1111-1111-1111-111111111111' WHERE [CoupleId] IS NULL AND [Role] <> 'SuperAdmin';");
            migrationBuilder.Sql("UPDATE [Contributions] SET [CreatedAtUtc] = [PaidAt] WHERE [CreatedAtUtc] = '0001-01-01T00:00:00.0000000';");
            migrationBuilder.Sql("UPDATE c SET c.[OrderId] = p.[OrderId], c.[GuestEmail] = p.[PayerEmail], c.[PaymentStatus] = p.[Status], c.[CoupleId] = p.[CoupleId] FROM [Contributions] c INNER JOIN [Payments] p ON p.[ContributionId] = c.[Id];");
            migrationBuilder.Sql("UPDATE [Gifts] SET [Category] = NULL WHERE LTRIM(RTRIM([Category])) = '';");

            migrationBuilder.CreateTable(
                name: "EmailOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    GiftName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CoupleNames = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderLookupAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IpHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmailHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Matched = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLookupAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentOrderLookupTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOrderLookupTokens", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("0ac3abdd-0c2d-4234-b72b-b327d8563af7"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("42fdcc72-664b-4d65-95e2-e8f4f906f28b"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("cbb2ebce-0130-4acc-aebc-054ca72cbfca"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("df173a4e-d8f8-472f-ae72-7b64e3e8f076"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.CreateIndex(
                name: "IX_Users_CoupleId",
                table: "Users",
                column: "CoupleId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CoupleId_Status_UpdatedAt",
                table: "Payments",
                columns: new[] { "CoupleId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_CoupleId",
                table: "Gifts",
                column: "CoupleId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_CoupleId_Status_PaidAt",
                table: "Contributions",
                columns: new[] { "CoupleId", "Status", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_OrderId",
                table: "Contributions",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CoupleId_CreatedAtUtc",
                table: "AuditLogs",
                columns: new[] { "CoupleId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_PaymentId_Type",
                table: "EmailOutboxMessages",
                columns: new[] { "PaymentId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_Status_NextAttemptAtUtc",
                table: "EmailOutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderLookupAttempts_EmailHash_CreatedAtUtc",
                table: "OrderLookupAttempts",
                columns: new[] { "EmailHash", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderLookupAttempts_IpHash_CreatedAtUtc",
                table: "OrderLookupAttempts",
                columns: new[] { "IpHash", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrderLookupTokens_PaymentId_ExpiresAtUtc",
                table: "PaymentOrderLookupTokens",
                columns: new[] { "PaymentId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrderLookupTokens_TokenHash",
                table: "PaymentOrderLookupTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "EmailOutboxMessages");

            migrationBuilder.DropTable(
                name: "OrderLookupAttempts");

            migrationBuilder.DropTable(
                name: "PaymentOrderLookupTokens");

            migrationBuilder.DropIndex(
                name: "IX_Users_CoupleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CoupleId_Status_UpdatedAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_CoupleId",
                table: "Gifts");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_CoupleId_Status_PaidAt",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_OrderId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "CoupleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CoupleId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CoupleId",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "CoupleId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Contributions");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Gifts",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);
        }
    }
}
