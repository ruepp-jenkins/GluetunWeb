using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixedPortBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortAllocations");

            migrationBuilder.DropColumn(
                name: "ListenHostPort",
                table: "LoadBalancers");

            migrationBuilder.DropColumn(
                name: "StateHostPort",
                table: "LoadBalancers");

            migrationBuilder.DropColumn(
                name: "ControlHostPort",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "HttpProxyHostPort",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ShadowsocksHostPort",
                table: "Connections");

            // Deliberately dropped and re-added rather than renamed (which is what EF scaffolded):
            // reusing an old per-purpose port as a block start would leave blocks unaligned and
            // overlapping — e.g. old SOCKS5 ports 20001 and 20004 would claim 20001-20008 and
            // 20004-20011. Starting at 0 means every owner is assigned a clean, aligned block on
            // its next deploy, and PortManager's live-Docker check keeps that safe while other
            // connections are still running on their old ports.
            migrationBuilder.DropColumn(
                name: "WebHostPort",
                table: "LoadBalancers");

            migrationBuilder.DropColumn(
                name: "Socks5HostPort",
                table: "Connections");

            migrationBuilder.AddColumn<int>(
                name: "PortBlockStart",
                table: "LoadBalancers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PortBlockStart",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortBlockStart",
                table: "LoadBalancers");

            migrationBuilder.DropColumn(
                name: "PortBlockStart",
                table: "Connections");

            migrationBuilder.AddColumn<int>(
                name: "WebHostPort",
                table: "LoadBalancers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Socks5HostPort",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ListenHostPort",
                table: "LoadBalancers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StateHostPort",
                table: "LoadBalancers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ControlHostPort",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HttpProxyHostPort",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShadowsocksHostPort",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PortAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    LoadBalancerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Purpose = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortAllocations_Connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PortAllocations_LoadBalancers_LoadBalancerId",
                        column: x => x.LoadBalancerId,
                        principalTable: "LoadBalancers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortAllocations_ConnectionId",
                table: "PortAllocations",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PortAllocations_LoadBalancerId",
                table: "PortAllocations",
                column: "LoadBalancerId");

            migrationBuilder.CreateIndex(
                name: "IX_PortAllocations_Port",
                table: "PortAllocations",
                column: "Port",
                unique: true);
        }
    }
}
