using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SABZ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCropRecommendationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HarvestDate",
                table: "Crops",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CropChangeRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreviousCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NextCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Effect = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropChangeRules", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CropChangeRules",
                columns: new[] { "Id", "Effect", "Explanation", "IsActive", "NextCategory", "PreviousCategory", "Source" },
                values: new object[,]
                {
                    { 1, "Positive", "Pulses are generally considered to leave residual soil nitrogen, which commonly benefits a following cereal crop.", true, "Cereal", "Pulse", "Initial SABZ crop-change reference dataset (general agronomic knowledge, expert review recommended)" },
                    { 2, "Positive", "Alternating cereals with pulses is widely considered sound rotation practice; pulses help maintain soil nitrogen.", true, "Pulse", "Cereal", "Initial SABZ crop-change reference dataset (general agronomic knowledge, expert review recommended)" },
                    { 3, "Caution", "Repeated cereal cropping can build up similar pests/diseases and draw on the same nutrients; rotating with a different crop group is commonly advised.", true, "Cereal", "Cereal", "Initial SABZ crop-change reference dataset (general agronomic knowledge, expert review recommended)" },
                    { 4, "Caution", "Growing vegetables back-to-back can increase pest and disease carry-over; rotating with a different crop group is commonly advised.", true, "Vegetable", "Vegetable", "Initial SABZ crop-change reference dataset (general agronomic knowledge, expert review recommended)" },
                    { 5, "Caution", "Consecutive pulse crops can build up pulse-specific diseases; alternating with another crop group is commonly advised.", true, "Pulse", "Pulse", "Initial SABZ crop-change reference dataset (general agronomic knowledge, expert review recommended)" },
                    { 6, "Positive", "Oilseeds are generally considered a good preceding crop for cereals due to different rooting and nutrient use.", true, "Cereal", "Oilseed", "Initial SABZ crop-change reference dataset (general agronomic knowledge, expert review recommended)" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropChangeRules_PreviousCategory_NextCategory",
                table: "CropChangeRules",
                columns: new[] { "PreviousCategory", "NextCategory" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropChangeRules");

            migrationBuilder.DropColumn(
                name: "HarvestDate",
                table: "Crops");
        }
    }
}
