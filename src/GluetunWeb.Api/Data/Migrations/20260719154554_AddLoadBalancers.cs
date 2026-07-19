using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadBalancers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ConnectionId",
                table: "PortAllocations",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "LoadBalancerId",
                table: "PortAllocations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Socks5BalancerImage",
                table: "GlobalSettings",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "LoadBalancers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UpstreamHost = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UpstreamSelectRule = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RetryTimes = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectTimeout = table.Column<int>(type: "INTEGER", nullable: false),
                    TestRemoteHost = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TestRemotePort = table.Column<int>(type: "INTEGER", nullable: false),
                    TcpCheckPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectCheckPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    AdditionCheckPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    ThreadNum = table.Column<int>(type: "INTEGER", nullable: false),
                    ServerChangeTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ListenHostPort = table.Column<int>(type: "INTEGER", nullable: false),
                    WebHostPort = table.Column<int>(type: "INTEGER", nullable: false),
                    StateHostPort = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadBalancers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoadBalancerUpstreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LoadBalancerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadBalancerUpstreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoadBalancerUpstreams_Connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoadBalancerUpstreams_LoadBalancers_LoadBalancerId",
                        column: x => x.LoadBalancerId,
                        principalTable: "LoadBalancers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortAllocations_LoadBalancerId",
                table: "PortAllocations",
                column: "LoadBalancerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoadBalancers_Identifier",
                table: "LoadBalancers",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoadBalancerUpstreams_ConnectionId",
                table: "LoadBalancerUpstreams",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_LoadBalancerUpstreams_LoadBalancerId",
                table: "LoadBalancerUpstreams",
                column: "LoadBalancerId");

            migrationBuilder.AddForeignKey(
                name: "FK_PortAllocations_LoadBalancers_LoadBalancerId",
                table: "PortAllocations",
                column: "LoadBalancerId",
                principalTable: "LoadBalancers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortAllocations_LoadBalancers_LoadBalancerId",
                table: "PortAllocations");

            migrationBuilder.DropTable(
                name: "LoadBalancerUpstreams");

            migrationBuilder.DropTable(
                name: "LoadBalancers");

            migrationBuilder.DropIndex(
                name: "IX_PortAllocations_LoadBalancerId",
                table: "PortAllocations");

            migrationBuilder.DropColumn(
                name: "LoadBalancerId",
                table: "PortAllocations");

            migrationBuilder.DropColumn(
                name: "Socks5BalancerImage",
                table: "GlobalSettings");

            migrationBuilder.AlterColumn<int>(
                name: "ConnectionId",
                table: "PortAllocations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
