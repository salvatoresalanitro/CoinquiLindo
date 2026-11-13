using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoinquiLindo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleaningTaskEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows left behind by the optional relationships (e.g. checklist items of deleted rotation shifts):
            // they belong to nothing and would otherwise become Guid.Empty foreign keys
            migrationBuilder.Sql("""DELETE FROM "ShiftChecklistItem" WHERE "ShiftId" IS NULL;""");
            migrationBuilder.Sql("""DELETE FROM "CleaningTasks" WHERE "RoomId" IS NULL;""");

            migrationBuilder.DropForeignKey(
                name: "FK_CleaningTasks_Rooms_RoomId",
                table: "CleaningTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftChecklistItem_Shifts_ShiftId",
                table: "ShiftChecklistItem");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShiftId",
                table: "ShiftChecklistItem",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CleaningTaskId",
                table: "ShiftChecklistItem",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoomId",
                table: "CleaningTasks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OneUnreadTaskSummaryPerHouse",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "Type", "HouseId" },
                unique: true,
                filter: "\"Type\" = 6 AND \"ReadAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CleaningTasks_Rooms_RoomId",
                table: "CleaningTasks",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftChecklistItem_Shifts_ShiftId",
                table: "ShiftChecklistItem",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Existing items were copies of the task description: link them to the task with the same description
            // in the room of their shift; items whose task was renamed or deleted in the meantime stay unlinked
            migrationBuilder.Sql("""
                UPDATE "ShiftChecklistItem"
                SET "CleaningTaskId" = (
                    SELECT "ct"."Id"
                    FROM "CleaningTasks" AS "ct"
                    INNER JOIN "Shifts" AS "s" ON "s"."AssignedRoomId" = "ct"."RoomId"
                    WHERE "s"."Id" = "ShiftChecklistItem"."ShiftId" AND "ct"."Description" = "ShiftChecklistItem"."Description"
                    LIMIT 1)
                WHERE "CleaningTaskId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CleaningTasks_Rooms_RoomId",
                table: "CleaningTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftChecklistItem_Shifts_ShiftId",
                table: "ShiftChecklistItem");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_OneUnreadTaskSummaryPerHouse",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CleaningTaskId",
                table: "ShiftChecklistItem");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShiftId",
                table: "ShiftChecklistItem",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoomId",
                table: "CleaningTasks",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_CleaningTasks_Rooms_RoomId",
                table: "CleaningTasks",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftChecklistItem_Shifts_ShiftId",
                table: "ShiftChecklistItem",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id");
        }
    }
}
