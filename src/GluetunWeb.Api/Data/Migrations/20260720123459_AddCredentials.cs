using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CredentialId",
                table: "Providers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CredentialId",
                table: "CustomVpnConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Credentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    VpnType = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenVpnUserEnc = table.Column<string>(type: "TEXT", nullable: true),
                    OpenVpnPasswordEnc = table.Column<string>(type: "TEXT", nullable: true),
                    WireGuardPrivateKeyEnc = table.Column<string>(type: "TEXT", nullable: true),
                    WireGuardPresharedKeyEnc = table.Column<string>(type: "TEXT", nullable: true),
                    WireGuardAddresses = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Providers_CredentialId",
                table: "Providers",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomVpnConfigs_CredentialId",
                table: "CustomVpnConfigs",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_Name",
                table: "Credentials",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomVpnConfigs_Credentials_CredentialId",
                table: "CustomVpnConfigs",
                column: "CredentialId",
                principalTable: "Credentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Providers_Credentials_CredentialId",
                table: "Providers",
                column: "CredentialId",
                principalTable: "Credentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomVpnConfigs_Credentials_CredentialId",
                table: "CustomVpnConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_Providers_Credentials_CredentialId",
                table: "Providers");

            migrationBuilder.DropTable(
                name: "Credentials");

            migrationBuilder.DropIndex(
                name: "IX_Providers_CredentialId",
                table: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_CustomVpnConfigs_CredentialId",
                table: "CustomVpnConfigs");

            migrationBuilder.DropColumn(
                name: "CredentialId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "CredentialId",
                table: "CustomVpnConfigs");
        }
    }
}
