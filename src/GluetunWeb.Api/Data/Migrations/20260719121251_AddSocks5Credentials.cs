using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocks5Credentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Socks5PasswordEnc",
                table: "Connections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Socks5User",
                table: "Connections",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Socks5PasswordEnc",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "Socks5User",
                table: "Connections");
        }
    }
}
