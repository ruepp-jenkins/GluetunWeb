using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShadowsocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableShadowsocks",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShadowsocksCipher",
                table: "Connections",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                // Backfill Gluetun's own default rather than "", so existing rows carry a valid
                // cipher instead of a blank the UI would render as an empty selection.
                defaultValue: "chacha20-ietf-poly1305");

            migrationBuilder.AddColumn<int>(
                name: "ShadowsocksHostPort",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShadowsocksLog",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShadowsocksPasswordEnc",
                table: "Connections",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableShadowsocks",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ShadowsocksCipher",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ShadowsocksHostPort",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ShadowsocksLog",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ShadowsocksPasswordEnc",
                table: "Connections");
        }
    }
}
