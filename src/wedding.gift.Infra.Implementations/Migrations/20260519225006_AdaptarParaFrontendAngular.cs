using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AdaptarParaFrontendAngular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Gifts",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Gifts",
                newName: "Image");

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Gifts",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Couples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Names = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WeddingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Couples", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("0ac3abdd-0c2d-4234-b72b-b327d8563af7"),
                column: "Total",
                value: 699.90m);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("42fdcc72-664b-4d65-95e2-e8f4f906f28b"),
                column: "Total",
                value: 1299.00m);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                column: "Total",
                value: 459.90m);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"),
                column: "Total",
                value: 329.99m);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("cbb2ebce-0130-4acc-aebc-054ca72cbfca"),
                column: "Total",
                value: 899.90m);

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("df173a4e-d8f8-472f-ae72-7b64e3e8f076"),
                column: "Total",
                value: 519.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Couples");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Gifts");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Gifts",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "Image",
                table: "Gifts",
                newName: "ImageUrl");
        }
    }
}
