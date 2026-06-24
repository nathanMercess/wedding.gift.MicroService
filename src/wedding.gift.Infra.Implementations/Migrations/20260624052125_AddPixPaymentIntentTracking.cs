using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddPixPaymentIntentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ContributionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_MpOrderId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.AddColumn<bool>(
                name: "ContributionCreated",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayerDocNumber",
                table: "Payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayerDocType",
                table: "Payments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayerEmail",
                table: "Payments",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ContributionId",
                table: "Payments",
                column: "ContributionId",
                unique: true,
                filter: "[ContributionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MpOrderId",
                table: "Payments",
                column: "MpOrderId",
                unique: true,
                filter: "[MpOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ContributionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_MpOrderId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ContributionCreated",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PayerDocNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PayerDocType",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PayerEmail",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ContributionId",
                table: "Payments",
                column: "ContributionId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MpOrderId",
                table: "Payments",
                column: "MpOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");
        }
    }
}
