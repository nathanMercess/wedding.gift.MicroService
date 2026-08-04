using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wedding.gift.Infra.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedGiftCoupleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("0ac3abdd-0c2d-4234-b72b-b327d8563af7"),
                column: "CoupleId",
                value: new Guid("1107b70c-c715-4672-9d51-73db9ed8d4fb"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("42fdcc72-664b-4d65-95e2-e8f4f906f28b"),
                column: "CoupleId",
                value: new Guid("1107b70c-c715-4672-9d51-73db9ed8d4fb"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                column: "CoupleId",
                value: new Guid("1107b70c-c715-4672-9d51-73db9ed8d4fb"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"),
                column: "CoupleId",
                value: new Guid("1107b70c-c715-4672-9d51-73db9ed8d4fb"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("cbb2ebce-0130-4acc-aebc-054ca72cbfca"),
                column: "CoupleId",
                value: new Guid("1107b70c-c715-4672-9d51-73db9ed8d4fb"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("df173a4e-d8f8-472f-ae72-7b64e3e8f076"),
                column: "CoupleId",
                value: new Guid("1107b70c-c715-4672-9d51-73db9ed8d4fb"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("0ac3abdd-0c2d-4234-b72b-b327d8563af7"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("42fdcc72-664b-4d65-95e2-e8f4f906f28b"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("cbb2ebce-0130-4acc-aebc-054ca72cbfca"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.UpdateData(
                table: "Gifts",
                keyColumn: "Id",
                keyValue: new Guid("df173a4e-d8f8-472f-ae72-7b64e3e8f076"),
                column: "CoupleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));
        }
    }
}
