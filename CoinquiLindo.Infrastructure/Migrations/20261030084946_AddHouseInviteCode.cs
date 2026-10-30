using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoinquiLindo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseInviteCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Houses",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            // Existing houses get a random code from the same alphabet as House.InviteCode (random() is evaluated per row and per call)
            const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var randomChar = $"substr('{alphabet}', (abs(random()) % {alphabet.Length}) + 1, 1)";
            migrationBuilder.Sql($"UPDATE \"Houses\" SET \"InviteCode\" = {string.Join(" || ", Enumerable.Repeat(randomChar, 8))};");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_InviteCode",
                table: "Houses",
                column: "InviteCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Houses_InviteCode",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Houses");
        }
    }
}
