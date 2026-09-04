using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SABZ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResetTehsilCoordinatesForCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reset all tehsil coordinates to NULL so the seeder
            // re-backfills them with the corrected district-centre data.
            migrationBuilder.Sql("UPDATE Tehsils SET Latitude = NULL, Longitude = NULL WHERE Latitude IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op — coordinates will be re-populated by the seeder.
        }
    }
}
