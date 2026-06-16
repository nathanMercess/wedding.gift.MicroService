using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MpOrderId",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MpPaymentId",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCodeBase64",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusDetail",
                table: "Payments",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MpOrderId",
                table: "Payments",
                column: "MpOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_MpOrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MpOrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MpPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "QrCodeBase64",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "StatusDetail",
                table: "Payments");
        }
    }
}
