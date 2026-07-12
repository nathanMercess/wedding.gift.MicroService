using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class PaymentIntentReceiptAndGiftFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Payments",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "DATEADD(minute, 15, SYSUTCDATETIME())");

            migrationBuilder.AddColumn<string>(
                name: "GiftName",
                table: "Payments",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE p
                SET
                    p.GiftName = g.Name,
                    p.ExpiresAt = DATEADD(minute, 15, p.CreatedAt)
                FROM Payments p
                INNER JOIN Gifts g ON g.Id = p.GiftId
                WHERE p.GiftName = '';
                """);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                column: "Category",
                value: "Eletrodomésticos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GiftName",
                table: "Payments");

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                column: "Category",
                value: "Eletrodomésticos");
        }
    }
}
