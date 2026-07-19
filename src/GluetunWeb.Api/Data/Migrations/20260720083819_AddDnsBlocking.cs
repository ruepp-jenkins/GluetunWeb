using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDnsBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BlockAds",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BlockMalicious",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                // Gluetun defaults BLOCK_MALICIOUS to "on". Backfilling false (what EF scaffolds
                // for a bool) would silently DISABLE protection that existing connections already
                // had, so existing rows must land on true.
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "BlockSurveillance",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DnsUnblockHostnames",
                table: "Connections",
                type: "TEXT",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockAds",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "BlockMalicious",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "BlockSurveillance",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "DnsUnblockHostnames",
                table: "Connections");
        }
    }
}
