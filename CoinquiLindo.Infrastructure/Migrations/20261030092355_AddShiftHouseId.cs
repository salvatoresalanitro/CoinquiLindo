using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoinquiLindo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftHouseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HouseId",
                table: "Shifts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Shifts"
                SET "HouseId" = (
                    SELECT "HouseId"
                    FROM "Rooms"
                    WHERE "Rooms"."Id" = "Shifts"."AssignedRoomId")
                WHERE "HouseId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "HouseId",
                table: "Shifts",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_HouseId",
                table: "Shifts",
                column: "HouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Houses_HouseId",
                table: "Shifts",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Houses_HouseId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_HouseId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "HouseId",
                table: "Shifts");
        }
    }
}
