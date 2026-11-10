using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoinquiLindo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RequireReportPhoto : Migration
    {
        // Same shape as ReportPhotoFormat.CreatePhotoKey(): 32 lowercase hex chars + .jpg/.png
        private static readonly string PhotoKeyGlob = string.Concat(Enumerable.Repeat("[0-9a-f]", 32)) + ".[jp][pn]g";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-014: legacy reports without a stored photo must be fixed by hand, never faked or deleted.
            // SQLite has no RAISE outside triggers: inserting offending rows into a CHECK(0) table aborts the migration.
            migrationBuilder.Sql($"""
                CREATE TEMP TABLE "__ReportPhotoGuard" (
                    "ReportId" TEXT CONSTRAINT "Reports_without_a_valid_photo_must_be_fixed_before_this_migration" CHECK (0)
                );
                INSERT INTO "__ReportPhotoGuard" ("ReportId")
                SELECT "Id" FROM "Reports"
                WHERE "PhotoUrl" IS NULL OR "PhotoUrl" NOT GLOB '{PhotoKeyGlob}';
                DROP TABLE "__ReportPhotoGuard";
                """);

            migrationBuilder.RenameColumn(
                name: "PhotoUrl",
                table: "Reports",
                newName: "PhotoKey");

            migrationBuilder.AlterColumn<string>(
                name: "PhotoKey",
                table: "Reports",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_PhotoKey_NotBlank",
                table: "Reports",
                sql: "length(trim(\"PhotoKey\")) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_PhotoKey_NotBlank",
                table: "Reports");

            migrationBuilder.AlterColumn<string>(
                name: "PhotoKey",
                table: "Reports",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);

            migrationBuilder.RenameColumn(
                name: "PhotoKey",
                table: "Reports",
                newName: "PhotoUrl");
        }
    }
}
