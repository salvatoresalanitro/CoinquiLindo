using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoinquiLindo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftDueDateAndHouseTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Shifts",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Houses",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "Europe/Rome");

            // Existing shifts stored instants: the day is their date part, and the last day used to be
            // considered active until its end. Deadline backfilled as end of that day in Rome, assuming
            // CET (UTC+1): summer shifts are off by one hour, acceptable for pre-release data.
            migrationBuilder.Sql("""
                UPDATE "Shifts"
                SET "DueDate" = strftime('%Y-%m-%d %H:%M:%S', substr("EndDate", 1, 10), '+1 day', '-1 hour');
                """);

            // StartDate/EndDate become calendar days (the column type stays TEXT)
            migrationBuilder.Sql("""
                UPDATE "Shifts"
                SET "StartDate" = substr("StartDate", 1, 10),
                    "EndDate" = substr("EndDate", 1, 10);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_DueDate",
                table: "Shifts",
                column: "DueDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shifts_DueDate",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Shifts");

            migrationBuilder.Sql("""
                UPDATE "Shifts"
                SET "StartDate" = "StartDate" || ' 00:00:00',
                    "EndDate" = "EndDate" || ' 00:00:00';
                """);

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Houses");
        }
    }
}
