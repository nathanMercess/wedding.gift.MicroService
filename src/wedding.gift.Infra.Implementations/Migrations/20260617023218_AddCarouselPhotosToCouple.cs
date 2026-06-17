using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddCarouselPhotosToCouple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarouselPhotosJson",
                table: "Couples",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarouselPhotosJson",
                table: "Couples");
        }
    }
}
