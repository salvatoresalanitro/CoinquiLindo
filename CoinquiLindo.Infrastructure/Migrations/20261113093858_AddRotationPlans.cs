using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoinquiLindo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRotationPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HelperTenantId",
                table: "Shifts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RotationPlanId",
                table: "Shifts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RotationPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TenantIds = table.Column<string>(type: "TEXT", nullable: false),
                    RoomIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotationPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RotationPlans_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_HelperTenantId",
                table: "Shifts",
                column: "HelperTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_OneRotationShiftPerRoomAndWeek",
                table: "Shifts",
                columns: new[] { "RotationPlanId", "AssignedRoomId", "StartDate" },
                unique: true,
                filter: "\"RotationPlanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RotationPlans_HouseId",
                table: "RotationPlans",
                column: "HouseId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Tenants_HelperTenantId",
                table: "Shifts",
                column: "HelperTenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Tenants_HelperTenantId",
                table: "Shifts");

            migrationBuilder.DropTable(
                name: "RotationPlans");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_HelperTenantId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_OneRotationShiftPerRoomAndWeek",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "HelperTenantId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "RotationPlanId",
                table: "Shifts");
        }
    }
}
