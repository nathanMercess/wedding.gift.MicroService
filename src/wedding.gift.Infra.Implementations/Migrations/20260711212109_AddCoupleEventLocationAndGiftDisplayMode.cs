using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddCoupleEventLocationAndGiftDisplayMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventLocation",
                table: "Couples",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GiftDisplayMode",
                table: "Couples",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Traditional");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventLocation",
                table: "Couples");

            migrationBuilder.DropColumn(
                name: "GiftDisplayMode",
                table: "Couples");
        }
    }
}
