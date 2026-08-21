using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SABZ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCropSuitabilityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TehsilId",
                table: "RegionalCropSuitabilities",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CropRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CropCatalogId = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GrowingDurationDays = table.Column<int>(type: "int", nullable: true),
                    MinTempC = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    MaxTempC = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    WaterRequirement = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SuitableSoils = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropRequirements_CropCatalog_CropCatalogId",
                        column: x => x.CropCatalogId,
                        principalTable: "CropCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CropCatalog",
                columns: new[] { "Id", "Category", "Description", "Name", "ScientificName" },
                values: new object[,]
                {
                    { 21, "Pulse", "Short-duration Kharif pulse, popular as catch crop and soil improver.", "Mung bean", "Vigna radiata" },
                    { 22, "Pulse", "Heat-tolerant Kharif pulse grown in Punjab and Sindh.", "Mash bean", "Vigna mungo" }
                });

            migrationBuilder.InsertData(
                table: "CropRequirements",
                columns: new[] { "Id", "CropCatalogId", "GrowingDurationDays", "MaxTempC", "MinTempC", "Season", "Source", "SuitableSoils", "WaterRequirement" },
                values: new object[,]
                {
                    { 1, 1, 150, 25m, 3m, "Rabi", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Loam,Loamy,Clay Loam,Sandy Loam,Alluvial", "Medium" },
                    { 2, 2, 130, 37m, 20m, "Kharif", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Clay,Clay Loam,Alluvial,Loam,Loamy", "High" },
                    { 3, 5, 110, 35m, 15m, "Kharif", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Loam,Loamy,Sandy Loam,Well-Drained", "Medium" },
                    { 4, 3, 160, 40m, 20m, "Kharif", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Loam,Loamy,Sandy Loam,Alluvial", "High" },
                    { 5, 4, 330, 38m, 20m, "Kharif", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Loam,Loamy,Clay Loam,Alluvial", "High" },
                    { 6, 12, 110, 28m, 5m, "Rabi", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Loam,Loamy,Sandy Loam,Clay Loam", "Low" },
                    { 7, 13, 120, 27m, 4m, "Rabi", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Loam,Loamy,Sandy Loam,Clay Loam", "Low" }
                });

            migrationBuilder.InsertData(
                table: "RegionalCropSuitabilities",
                columns: new[] { "Id", "CropCatalogId", "DistrictId", "Notes", "ProvinceId", "Season", "Source", "SuitabilityLevel", "SuitabilityScore", "TehsilId" },
                values: new object[,]
                {
                    { 1, 1, 102, "Faisalabad is in the heart of Punjab's wheat belt with fertile alluvial soil.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9, null },
                    { 2, 1, 105, "Sahiwal division is a major wheat producing region.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9, null },
                    { 3, 1, 104, "Southern Punjab wheat with irrigation support.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 4, 1, 101, "Lahore district grows wheat on irrigated alluvial soils.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 5, 1, 103, "Rawalpindi grows wheat in the Potohar rainfed/irrigated mix.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 6, 2, 106, "Sialkot-Gujranwala belt is famous for Basmati rice.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9, null },
                    { 7, 2, 107, "Gujranwala is a major rice growing district.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9, null },
                    { 8, 3, 104, "Multan is in Pakistan's cotton belt.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9, null },
                    { 9, 3, 108, "Bahawalpur division is a major cotton area.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9, null },
                    { 10, 3, 105, "Sahiwal has good cotton suitability.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 11, 4, 102, "Faisalabad region has multiple sugar mills nearby.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 12, 4, 109, "Jhang has good sugarcane growing conditions.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 13, 12, 103, "Potohar tract (Rawalpindi) is a traditional gram growing area.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 14, 12, 109, "Jhang supports gram on lighter soils.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 15, 13, 103, "Rainfed Potohar areas grow lentil (masoor).", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 18, 2, 243, "Larkana is famous for Sindh rice varieties.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9, null },
                    { 19, 2, 254, "Sukkur barrage supports rice cultivation.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 20, 3, 250, "Shaheed Benazir Abad (Nawabshah) area supports cotton growing.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 21, 4, 232, "Badin has sugar mills and sugarcane farms.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 23, 5, 228, "Swat valley is a major maize growing area.", 6, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9, null },
                    { 24, 5, 219, "Mardan supports maize cultivation.", 6, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 25, 1, 224, "Peshawar valley supports wheat cultivation.", 6, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 26, 1, 219, "Mardan has good wheat growing conditions.", 6, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 27, 1, 177, "Sibi has moderate wheat suitability with irrigation.", 3, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Moderate", 5, null },
                    { 28, 1, null, "Punjab is Pakistan's largest wheat producing province.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 29, 2, null, "Punjab grows rice widely in the central and northeast belts.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 30, 5, null, "Punjab grows maize but KP is the leading province.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Moderate", 6, null },
                    { 31, 3, null, "Southern and central Punjab form the national cotton belt.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 32, 4, null, "Punjab is the main sugarcane producing province.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 33, 12, null, "Punjab's rainfed tracts are traditional gram areas.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 34, 13, null, "Lentil is grown in northern Punjab rainfed areas.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Moderate", 6, null },
                    { 37, 2, null, "Sindh grows rice extensively along the Indus.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 38, 3, null, "Sindh is a major cotton producing province.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 39, 4, null, "Sindh supports sugarcane near sugar mills.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 41, 5, null, "KP is Pakistan's leading maize province.", 6, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8, null },
                    { 42, 1, null, "KP grows wheat in the Peshawar and southern valleys.", 6, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Moderate", 6, null },
                    { 43, 1, null, "Balochistan grows wheat in irrigated highland areas.", 3, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Moderate", 5, null },
                    { 44, 12, null, "Balochistan highlands support gram under rainfall.", 3, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Moderate", 5, null }
                });

            migrationBuilder.InsertData(
                table: "CropRequirements",
                columns: new[] { "Id", "CropCatalogId", "GrowingDurationDays", "MaxTempC", "MinTempC", "Season", "Source", "SuitableSoils", "WaterRequirement" },
                values: new object[,]
                {
                    { 8, 21, 70, 38m, 20m, "Kharif", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Loam,Loamy,Sandy Loam", "Low" },
                    { 9, 22, 80, 40m, 20m, "Kharif", "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)", "Sandy Loam,Loam,Loamy", "Low" }
                });

            migrationBuilder.InsertData(
                table: "RegionalCropSuitabilities",
                columns: new[] { "Id", "CropCatalogId", "DistrictId", "Notes", "ProvinceId", "Season", "Source", "SuitabilityLevel", "SuitabilityScore", "TehsilId" },
                values: new object[,]
                {
                    { 16, 21, 108, "Southern Punjab grows mung bean as a short Kharif pulse.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 17, 22, 104, "Multan region supports heat-tolerant mash bean.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 22, 21, 232, "Badin grows mung bean on coastal plain soils.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 35, 21, null, "Punjab is the main mung bean producing province.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null },
                    { 36, 22, null, "Mash bean is grown in southern Punjab.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Moderate", 6, null },
                    { 40, 21, null, "Sindh grows mung bean on lighter soils.", 7, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegionalCropSuitabilities_TehsilId",
                table: "RegionalCropSuitabilities",
                column: "TehsilId");

            migrationBuilder.CreateIndex(
                name: "IX_CropRequirements_CropCatalogId",
                table: "CropRequirements",
                column: "CropCatalogId");

            migrationBuilder.AddForeignKey(
                name: "FK_RegionalCropSuitabilities_Tehsils_TehsilId",
                table: "RegionalCropSuitabilities",
                column: "TehsilId",
                principalTable: "Tehsils",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegionalCropSuitabilities_Tehsils_TehsilId",
                table: "RegionalCropSuitabilities");

            migrationBuilder.DropTable(
                name: "CropRequirements");

            migrationBuilder.DropIndex(
                name: "IX_RegionalCropSuitabilities_TehsilId",
                table: "RegionalCropSuitabilities");

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "RegionalCropSuitabilities",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "CropCatalog",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "CropCatalog",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DropColumn(
                name: "TehsilId",
                table: "RegionalCropSuitabilities");
        }
    }
}
