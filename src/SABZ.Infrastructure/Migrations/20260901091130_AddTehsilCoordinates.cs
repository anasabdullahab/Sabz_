using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SABZ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTehsilCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Tehsils",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Tehsils",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Tehsils");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Tehsils");
        }
    }
}
