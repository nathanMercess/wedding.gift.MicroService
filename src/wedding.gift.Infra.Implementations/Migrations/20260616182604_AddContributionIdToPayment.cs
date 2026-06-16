using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionIdToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContributionId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ContributionId",
                table: "Payments",
                column: "ContributionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Contributions_ContributionId",
                table: "Payments",
                column: "ContributionId",
                principalTable: "Contributions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Contributions_ContributionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ContributionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ContributionId",
                table: "Payments");
        }
    }
}
