using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPortForwardingAndFirewall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirewallOutboundSubnets",
                table: "Connections",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirewallVpnInputPorts",
                table: "Connections",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PortForwarding",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PortForwardingPortsCount",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                // Gluetun's default is 1 port; 0 (what EF scaffolds for an int) is not a valid count
                // and would render as "0 ports" in the UI.
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "PortForwardingProvider",
                table: "Connections",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WireGuardMtu",
                table: "Connections",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirewallOutboundSubnets",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "FirewallVpnInputPorts",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "PortForwarding",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "PortForwardingPortsCount",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "PortForwardingProvider",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "WireGuardMtu",
                table: "Connections");
        }
    }
}
