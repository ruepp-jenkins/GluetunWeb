using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomVpnCredsAndDns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndpointDnsName",
                table: "CustomVpnConfigs",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenVpnPasswordEnc",
                table: "CustomVpnConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenVpnUserEnc",
                table: "CustomVpnConfigs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndpointDnsName",
                table: "CustomVpnConfigs");

            migrationBuilder.DropColumn(
                name: "OpenVpnPasswordEnc",
                table: "CustomVpnConfigs");

            migrationBuilder.DropColumn(
                name: "OpenVpnUserEnc",
                table: "CustomVpnConfigs");
        }
    }
}
