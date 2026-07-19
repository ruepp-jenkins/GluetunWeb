using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomVpnConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    VpnType = table.Column<int>(type: "INTEGER", nullable: false),
                    RawConfigEnc = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomVpnConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timezone = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PublicIpApi = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PublicIpApiTokenEnc = table.Column<string>(type: "TEXT", nullable: true),
                    HttpProxyEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    HttpProxyUser = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    HttpProxyPasswordEnc = table.Column<string>(type: "TEXT", nullable: true),
                    HttpProxyListeningAddress = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    HttpProxyStealth = table.Column<bool>(type: "INTEGER", nullable: false),
                    HttpProxyLog = table.Column<bool>(type: "INTEGER", nullable: false),
                    ControlServerAuth = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlServerUser = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ControlServerPasswordEnc = table.Column<string>(type: "TEXT", nullable: true),
                    ControlServerApiKeyEnc = table.Column<string>(type: "TEXT", nullable: true),
                    DockerHost = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    GluetunImage = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Socks5Image = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PortRangeStart = table.Column<int>(type: "INTEGER", nullable: false),
                    PortRangeEnd = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    VpnType = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenVpnUserEnc = table.Column<string>(type: "TEXT", nullable: true),
                    OpenVpnPasswordEnc = table.Column<string>(type: "TEXT", nullable: true),
                    WireGuardPrivateKeyEnc = table.Column<string>(type: "TEXT", nullable: true),
                    WireGuardPresharedKeyEnc = table.Column<string>(type: "TEXT", nullable: true),
                    WireGuardAddresses = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ServerCountries = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ServerCities = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ServerRegions = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ServerHostnames = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Connections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomVpnConfigId = table.Column<int>(type: "INTEGER", nullable: true),
                    ServerCountriesOverride = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ServerCitiesOverride = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ServerHostnamesOverride = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EnableSocks5 = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableHttpProxy = table.Column<bool>(type: "INTEGER", nullable: false),
                    Socks5HostPort = table.Column<int>(type: "INTEGER", nullable: false),
                    HttpProxyHostPort = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlHostPort = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Socks5ContainerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Connections_CustomVpnConfigs_CustomVpnConfigId",
                        column: x => x.CustomVpnConfigId,
                        principalTable: "CustomVpnConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Connections_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Purpose = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectionId = table.Column<int>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_CustomVpnConfigId",
                table: "Connections",
                column: "CustomVpnConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Connections_Identifier",
                table: "Connections",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Connections_ProviderId",
                table: "Connections",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomVpnConfigs_Name",
                table: "CustomVpnConfigs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortAllocations_ConnectionId",
                table: "PortAllocations",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PortAllocations_Port",
                table: "PortAllocations",
                column: "Port",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Providers_Name",
                table: "Providers",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "GlobalSettings");

            migrationBuilder.DropTable(
                name: "PortAllocations");

            migrationBuilder.DropTable(
                name: "Connections");

            migrationBuilder.DropTable(
                name: "CustomVpnConfigs");

            migrationBuilder.DropTable(
                name: "Providers");
        }
    }
}
