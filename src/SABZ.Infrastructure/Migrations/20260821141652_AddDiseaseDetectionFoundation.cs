using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SABZ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiseaseDetectionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiseaseInformations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiseaseName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CropCatalogId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Symptoms = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecommendedActions = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Prevention = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Monitoring = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiseaseInformations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiseaseInformations_CropCatalog_CropCatalogId",
                        column: x => x.CropCatalogId,
                        principalTable: "CropCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "DiseaseInformations",
                columns: new[] { "Id", "CropCatalogId", "Description", "DiseaseName", "IsActive", "Monitoring", "Prevention", "RecommendedActions", "Source", "Symptoms" },
                values: new object[,]
                {
                    { 1, 1, "A common fungal disease of wheat that appears as small round brown-orange pustules scattered on leaf surfaces and can reduce grain filling when severe.", "Wheat Leaf Rust", true, "Check lower and middle leaves twice weekly during tillering to grain fill; record whether pustules are increasing or spreading to new leaves", "Grow rust-resistant varieties where available; avoid very dense stands; monitor fields regularly during humid weather", "If only a few leaves are affected, remove and destroy them away from the field; if spreading, consult the local agricultural extension office promptly; avoid late-season excess nitrogen which can worsen rust", "Initial SABZ disease reference dataset (general plant-health knowledge, expert review recommended)", "Small round orange-brown pustules on leaves; yellowing around pustules; premature leaf drying in severe cases." },
                    { 2, 2, "A major fungal disease of rice causing diamond-shaped lesions on leaves and can affect necks of panicles, especially under warm humid conditions.", "Rice Blast", true, "Inspect leaves weekly during warm humid periods; watch for new diamond-shaped lesions and neck symptoms near flowering", "Use certified seed and resistant varieties; avoid prolonged leaf wetness; keep balanced fertility", "Avoid excess nitrogen application; maintain balanced water management; consult the local agricultural extension office if lesions are spreading", "Initial SABZ disease reference dataset (general plant-health knowledge, expert review recommended)", "Diamond/eye-shaped grey lesions with brown borders on leaves; lesions on nodes; neck rot in severe cases." },
                    { 3, 7, "A common fungal leaf disease of tomato showing dark concentric target-like spots, usually starting on older lower leaves.", "Tomato Early Blight", true, "Check lower leaves twice weekly, especially after rain; note whether spots are moving upward on the plant", "Mulch soil to reduce splash; rotate away from tomato/potato; water at the base of plants in the morning", "Remove affected lower leaves and dispose away from the field; improve air circulation; avoid wetting leaves when irrigating", "Initial SABZ disease reference dataset (general plant-health knowledge, expert review recommended)", "Dark brown spots with concentric rings (target-like) on older leaves; yellow halo around spots; lower leaves drying first." },
                    { 4, 7, "A virus disease spread by whiteflies causing upward curling, crinkling and yellowing of tomato leaves and reduced fruit set.", "Tomato Leaf Curl Virus", true, "Look weekly for new curling or yellowing plants and for whiteflies on leaf undersides", "Use healthy transplants; monitor and manage whiteflies early; consider reflective mulches where practical", "Remove clearly affected plants to reduce spread; control whitefly populations with guidance from a local expert; avoid moving plant material between fields", "Initial SABZ disease reference dataset (general plant-health knowledge, expert review recommended)", "Upward curling and crinkling of leaves; yellowing; stunted growth; poor fruit setting." },
                    { 5, 6, "A serious disease of potato favoured by cool wet weather, causing water-soaked dark lesions on leaves that can spread rapidly through a field.", "Potato Late Blight", true, "Inspect fields every 2-3 days during cool wet spells; check leaf undersides for white mould", "Use certified seed; avoid overhead irrigation late in the day; ensure good spacing for airflow", "Act quickly - remove and destroy affected foliage if limited; seek expert advice immediately if spreading, as late blight can escalate within days", "Initial SABZ disease reference dataset (general plant-health knowledge, expert review recommended)", "Water-soaked dark patches on leaf tips and edges; white mould under leaves in humid weather; rapid browning and collapse." },
                    { 6, 3, "A virus disease of cotton spread by whiteflies, causing leaf curling, vein thickening and enations, and significant yield loss in susceptible varieties.", "Cotton Leaf Curl Virus", true, "Check young plants weekly for curling and enations; monitor whitefly numbers on leaf undersides", "Use tolerant varieties where available; manage whitefly populations early; keep fields free of alternate hosts", "Remove clearly affected plants early; manage whiteflies following local expert guidance; avoid late sowing where the disease is known to be common", "Initial SABZ disease reference dataset (general plant-health knowledge, expert review recommended)", "Upward/downward curling of leaves; thickened veins; leaf-like outgrowths (enations) on leaf undersides; stunted plants." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseInformations_CropCatalogId",
                table: "DiseaseInformations",
                column: "CropCatalogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiseaseInformations");
        }
    }
}
