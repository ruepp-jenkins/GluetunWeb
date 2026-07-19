using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GluetunWeb.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBlockSurveillance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockSurveillance",
                table: "Connections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BlockSurveillance",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
