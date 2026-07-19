using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateBalancerPortRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BalancerPortRangeEnd",
                table: "GlobalSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BalancerPortRangeStart",
                table: "GlobalSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalancerPortRangeEnd",
                table: "GlobalSettings");

            migrationBuilder.DropColumn(
                name: "BalancerPortRangeStart",
                table: "GlobalSettings");
        }
    }
}
