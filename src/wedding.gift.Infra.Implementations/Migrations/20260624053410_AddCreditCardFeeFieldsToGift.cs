using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditCardFeeFieldsToGift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditCardFeePercent",
                table: "Gifts",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CreditCardMaxInstallments",
                table: "Gifts",
                type: "int",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("0ac3abdd-0c2d-4234-b72b-b327d8563af7"),
                column: "CreditCardMaxInstallments",
                value: 12);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("42fdcc72-664b-4d65-95e2-e8f4f906f28b"),
                column: "CreditCardMaxInstallments",
                value: 12);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                column: "CreditCardMaxInstallments",
                value: 12);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"),
                column: "CreditCardMaxInstallments",
                value: 12);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("cbb2ebce-0130-4acc-aebc-054ca72cbfca"),
                column: "CreditCardMaxInstallments",
                value: 12);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("df173a4e-d8f8-472f-ae72-7b64e3e8f076"),
                column: "CreditCardMaxInstallments",
                value: 12);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditCardFeePercent",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "CreditCardMaxInstallments",
                table: "Gifts");
        }
    }
}
