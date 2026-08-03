using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoupleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestConfirmations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuestInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoupleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfirmedGuests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestConfirmationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSubmitter = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmedGuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfirmedGuests_GuestConfirmations_GuestConfirmationId",
                        column: x => x.GuestConfirmationId,
                        principalTable: "GuestConfirmations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConfirmedGuests_GuestInvitations_GuestInvitationId",
                        column: x => x.GuestInvitationId,
                        principalTable: "GuestInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmedGuests_GuestConfirmationId",
                table: "ConfirmedGuests",
                column: "GuestConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmedGuests_GuestInvitationId",
                table: "ConfirmedGuests",
                column: "GuestInvitationId",
                unique: true,
                filter: "[GuestInvitationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GuestConfirmations_CoupleId_ConfirmedAtUtc",
                table: "GuestConfirmations",
                columns: new[] { "CoupleId", "ConfirmedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestInvitations_CoupleId_IsActive",
                table: "GuestInvitations",
                columns: new[] { "CoupleId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestInvitations_CoupleId_NormalizedName",
                table: "GuestInvitations",
                columns: new[] { "CoupleId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfirmedGuests");

            migrationBuilder.DropTable(
                name: "GuestConfirmations");

            migrationBuilder.DropTable(
                name: "GuestInvitations");
        }
    }
}
