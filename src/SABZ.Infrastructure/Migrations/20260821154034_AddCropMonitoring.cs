using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SABZ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCropMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CropMonitoringRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CropCatalogId = table.Column<int>(type: "int", nullable: true),
                    DayOffsetAfterPlanting = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InspectionItems = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropMonitoringRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropMonitoringRules_CropCatalog_CropCatalogId",
                        column: x => x.CropCatalogId,
                        principalTable: "CropCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CropMonitoringChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<int>(type: "int", nullable: true),
                    FarmId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InspectionItems = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FarmerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SkippedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropMonitoringChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropMonitoringChecks_CropMonitoringRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "CropMonitoringRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CropMonitoringChecks_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CropMonitoringRules",
                columns: new[] { "Id", "CropCatalogId", "DayOffsetAfterPlanting", "Description", "InspectionItems", "IsActive", "Priority", "Source", "Title", "TriggerType" },
                values: new object[,]
                {
                    { 1, 1, 14, "Check that seedlings have emerged evenly and look for early signs of stress or damage in several places across the field.", "Even seedling emergence; yellowing of young leaves; unusual spots; insect feeding on seedlings; weed seedlings", true, "Medium", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Early growth and emergence check", "Scheduled" },
                    { 2, 1, 30, "Examine leaves on several plants for spots, discoloration and insect damage, and check how weeds are competing with the crop.", "Leaf spots; yellowing or rust-coloured pustules; holes or insect damage; weed competition; stunted plants", true, "High", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Leaf health and pest check", "Scheduled" },
                    { 3, 1, 60, "Walk the field and inspect the middle and upper leaves for disease symptoms and overall crop condition.", "Rust pustules or stripes; powdery patches; pest damage; wilting or weak stems; general crop vigour", true, "Medium", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Mid-season disease and crop health check", "Scheduled" },
                    { 4, 2, 15, "Check seedling establishment and look for early problems in the paddy.", "Even stand; missing or dead seedlings; yellowing; snails or insects; weed growth", true, "Medium", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Establishment check", "Scheduled" },
                    { 5, 2, 35, "Inspect leaves on several hills for lesions and insects, especially during warm humid weather.", "Diamond-shaped or oval leaf lesions; yellowing; leaf folders or stem borers; brown planthopper insects at the base", true, "High", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Leaf disease and pest check", "Scheduled" },
                    { 6, 2, 70, "Check the crop around flowering for diseases and grain development problems.", "Neck or node lesions; discoloured grains; pest damage; uneven flowering; weed escapes", true, "Medium", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Flowering-stage health check", "Scheduled" },
                    { 7, 3, 20, "Inspect young plants for vigour and early leaf-curl symptoms spread by whiteflies.", "Curling or crinkled leaves; thickened veins; whiteflies on leaf undersides; missing plants; insect damage", true, "High", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Seedling and leaf curl check", "Scheduled" },
                    { 8, 3, 45, "Check leaves, buds and squares for bollworms, aphids, whiteflies and mites.", "Holes in buds or squares; sticky honeydew; curled or bronzed leaves; pest eggs or larvae on undersides", true, "High", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Pest pressure check", "Scheduled" },
                    { 9, 3, 90, "Inspect developing bolls and upper leaves for pests, disease symptoms and plant health.", "Damaged or dropped bolls; leaf spots; wilting branches; pest activity; overall plant vigour", true, "Medium", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Mid-season boll health check", "Scheduled" },
                    { 10, 6, 14, "Check that plants have emerged evenly and look for early leaf problems.", "Even emergence; missing plants; dark or water-soaked leaf patches; insect feeding; weed seedlings", true, "Medium", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Emergence check", "Scheduled" },
                    { 11, 6, 30, "Inspect leaf tips and undersides during cool wet weather for early blight symptoms and pests.", "Water-soaked dark patches; white mould under leaves; holes or larvae; yellowing; weed competition", true, "High", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Blight and pest check", "Scheduled" },
                    { 12, 6, 55, "Check foliage health as tubers develop; late blight can escalate within days, so inspect carefully.", "Rapid browning or collapse; spreading lesions; pest damage; wilting; overall canopy condition", true, "High", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Tuber-stage health check", "Scheduled" },
                    { 13, 7, 14, "Check transplants for establishment and early leaf problems.", "Wilting or dead transplants; dark spots on lower leaves; curling; cutworm damage; weed seedlings", true, "Medium", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Transplant establishment check", "Scheduled" },
                    { 14, 7, 30, "Examine leaves, especially the lower ones, for target-like spots and pests.", "Dark concentric spots; yellowing around spots; holes from caterpillars; aphids or whiteflies; curling leaves", true, "High", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Leaf spot and pest check", "Scheduled" },
                    { 15, 7, 60, "Inspect foliage and developing fruit during fruiting for disease and damage.", "Spots moving up the plant; fruit blemishes; wilting; leaf mould under humid conditions; pest damage on fruit", true, "Medium", "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)", "Fruiting-stage health check", "Scheduled" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropMonitoringChecks_CropId_RuleId",
                table: "CropMonitoringChecks",
                columns: new[] { "CropId", "RuleId" },
                unique: true,
                filter: "[RuleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CropMonitoringChecks_FarmId",
                table: "CropMonitoringChecks",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_CropMonitoringChecks_RuleId",
                table: "CropMonitoringChecks",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CropMonitoringChecks_ScheduledDate",
                table: "CropMonitoringChecks",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_CropMonitoringRules_CropCatalogId",
                table: "CropMonitoringRules",
                column: "CropCatalogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropMonitoringChecks");

            migrationBuilder.DropTable(
                name: "CropMonitoringRules");
        }
    }
}
