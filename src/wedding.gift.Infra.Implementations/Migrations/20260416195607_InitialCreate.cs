using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Gifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContributorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contributions_Gifts_GiftId",
                        column: x => x.GiftId,
                        principalTable: "Gifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Gifts",
                columns: new[] { "Id", "Available", "Category", "CreatedAt", "Description", "ImageUrl", "Price", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("0ac3abdd-0c2d-4234-b72b-b327d8563af7"), true, "Cozinha", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Cafeteira automática para cápsulas e pó.", "https://images.example.com/cafeteira.jpg", 699.90m, "Cafeteira Expresso", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("42fdcc72-664b-4d65-95e2-e8f4f906f28b"), true, "Casa", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Aspirador inteligente com base carregadora.", "https://images.example.com/aspirador-robo.jpg", 1299.00m, "Aspirador Robô", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"), true, "Eletrodomésticos", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Liquidificador potente com copo de vidro.", "https://images.example.com/liquidificador.jpg", 459.90m, "Liquidificador 1200W", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"), true, "Quarto", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Kit completo 400 fios para cama queen.", "https://images.example.com/jogo-cama.jpg", 329.99m, "Jogo de Cama Queen", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("cbb2ebce-0130-4acc-aebc-054ca72cbfca"), true, "Cozinha", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Conjunto com 5 panelas em inox para o dia a dia.", "https://images.example.com/panelas-inox.jpg", 899.90m, "Jogo de Panelas Inox", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("df173a4e-d8f8-472f-ae72-7b64e3e8f076"), true, "Mesa", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Conjunto de jantar em porcelana branca.", "https://images.example.com/aparelho-jantar.jpg", 519.00m, "Aparelho de Jantar 20 Peças", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_GiftId",
                table: "Contributions",
                column: "GiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contributions");

            migrationBuilder.DropTable(
                name: "Gifts");
        }
    }
}
