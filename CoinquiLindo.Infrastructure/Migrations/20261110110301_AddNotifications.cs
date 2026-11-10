using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoinquiLindo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    HouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShiftId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReportId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DueSoonOncePerShift",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "Type", "ShiftId" },
                unique: true,
                filter: "\"Type\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_HouseId",
                table: "Notifications",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OneUnreadVoteSummaryPerReport",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "Type", "ReportId" },
                unique: true,
                filter: "\"Type\" = 4 AND \"ReadAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_ReadAt_CreatedAt",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "ReadAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
