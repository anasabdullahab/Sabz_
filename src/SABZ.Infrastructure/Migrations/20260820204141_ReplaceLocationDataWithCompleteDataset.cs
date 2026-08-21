using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SABZ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLocationDataWithCompleteDataset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty.
            // Province, District, and Tehsil HasData entries have been removed from the model
            // but the existing database rows are PRESERVED (not deleted) so that
            // foreign keys from Farms remain valid.
            // The runtime LocationDataSeeder inserts the complete new dataset on startup.
            // RegionalCropSuitability HasData is also removed; it will be re-added later.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Provinces",
                columns: new[] { "Id", "Name", "NameUrdu" },
                values: new object[,]
                {
                    { 1, "Punjab", "پنجاب" },
                    { 2, "Sindh", "سندھ" },
                    { 3, "Khyber Pakhtunkhwa", "خیبر پختونخوا" },
                    { 4, "Balochistan", "بلوچستان" },
                    { 5, "Islamabad Capital Territory", "وفاقی دارالحکومت" },
                    { 6, "Gilgit-Baltistan", "گلگت بلتستان" },
                    { 7, "Azad Jammu and Kashmir", "آزاد جموں و کشمیر" }
                });

            migrationBuilder.InsertData(
                table: "Districts",
                columns: new[] { "Id", "Name", "NameUrdu", "ProvinceId" },
                values: new object[,]
                {
                    { 101, "Lahore", null, 1 },
                    { 102, "Faisalabad", null, 1 },
                    { 103, "Rawalpindi", null, 1 },
                    { 104, "Multan", null, 1 },
                    { 105, "Sahiwal", null, 1 },
                    { 106, "Sialkot", null, 1 },
                    { 107, "Gujranwala", null, 1 },
                    { 108, "Bahawalpur", null, 1 },
                    { 109, "Jhang", null, 1 },
                    { 110, "Okara", null, 1 },
                    { 201, "Karachi South", null, 2 },
                    { 202, "Hyderabad", null, 2 },
                    { 203, "Sukkur", null, 2 },
                    { 204, "Larkana", null, 2 },
                    { 205, "Nawabshah", null, 2 },
                    { 206, "Mirpur Khas", null, 2 },
                    { 207, "Thatta", null, 2 },
                    { 208, "Badin", null, 2 },
                    { 301, "Peshawar", null, 3 },
                    { 302, "Mardan", null, 3 },
                    { 303, "Swat", null, 3 },
                    { 304, "Abbottabad", null, 3 },
                    { 305, "Dera Ismail Khan", null, 3 },
                    { 306, "Mansehra", null, 3 },
                    { 401, "Quetta", null, 4 },
                    { 402, "Gwadar", null, 4 },
                    { 403, "Khuzdar", null, 4 },
                    { 404, "Sibi", null, 4 },
                    { 405, "Turbat", null, 4 },
                    { 501, "Islamabad", null, 5 },
                    { 601, "Gilgit", null, 6 },
                    { 602, "Skardu", null, 6 },
                    { 701, "Muzaffarabad", null, 7 },
                    { 702, "Mirpur", null, 7 }
                });

            migrationBuilder.InsertData(
                table: "RegionalCropSuitabilities",
                columns: new[] { "Id", "CropCatalogId", "DistrictId", "Notes", "ProvinceId", "Season", "Source", "SuitabilityLevel", "SuitabilityScore" },
                values: new object[,]
                {
                    { 1, 1, 102, "Faisalabad is in the heart of Punjab's wheat belt with fertile alluvial soil.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 2, 1, 105, "Sahiwal division is a major wheat producing region.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 3, 1, 104, "Southern Punjab wheat with irrigation support.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 4, 2, 106, "Sialkot-Gujranwala belt is famous for Basmati rice.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 5, 2, 107, "Gujranwala is a major rice growing district.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 6, 3, 104, "Multan is in Pakistan's cotton belt.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 7, 3, 108, "Bahawalpur division is a major cotton area.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 8, 3, 105, "Sahiwal has good cotton suitability.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 9, 4, 102, "Faisalabad region has multiple sugar mills nearby.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 10, 4, 109, "Jhang has good sugarcane growing conditions.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7 },
                    { 11, 11, 109, "Jhang/Sargodha belt is world-famous for Kinnow mandarin.", 1, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 12, 10, 104, "Multan is the mango capital of Pakistan.", 1, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 10 },
                    { 13, 2, 204, "Larkana is famous for Sindh rice varieties.", 2, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 14, 2, 203, "Sukkur barrage supports rice cultivation.", 2, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7 },
                    { 15, 3, 205, "Nawabshah area supports cotton growing.", 2, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7 },
                    { 16, 4, 208, "Badin has sugar mills and sugarcane farms.", 2, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 17, 9, 206, "Mirpur Khas/Kunri is the chili capital of Pakistan.", 2, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 18, 9, 208, "Badin is a major chili growing district.", 2, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 19, 18, 203, "Sukkur/Khairpur is a major date producing region.", 2, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 20, 5, 303, "Swat valley is a major maize growing area.", 3, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 21, 5, 302, "Mardan supports maize cultivation.", 3, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 22, 17, 302, "Mardan/Swat is the tobacco belt of Pakistan.", 3, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 23, 17, 303, "Swat valley has tobacco farms.", 3, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 24, 1, 301, "Peshawar valley supports wheat cultivation.", 3, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7 },
                    { 25, 1, 302, "Mardan has good wheat growing conditions.", 3, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 7 },
                    { 26, 19, 401, "Quetta/Ziarat is famous for apple orchards.", 4, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Very High", 9 },
                    { 27, 18, 405, "Turbat/Kech is a major date producing area.", 4, "Kharif", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "High", 8 },
                    { 28, 1, 404, "Sibi has moderate wheat suitability with irrigation.", 4, "Rabi", "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)", "Moderate", 5 }
                });

            migrationBuilder.InsertData(
                table: "Tehsils",
                columns: new[] { "Id", "DistrictId", "Name", "NameUrdu" },
                values: new object[,]
                {
                    { 1001, 101, "Lahore City", null },
                    { 1002, 101, "Model Town", null },
                    { 1003, 101, "Shalimar", null },
                    { 1004, 102, "Faisalabad City", null },
                    { 1005, 102, "Jaranwala", null },
                    { 1006, 102, "Tandlianwala", null },
                    { 1007, 103, "Rawalpindi", null },
                    { 1008, 103, "Gujar Khan", null },
                    { 1009, 103, "Taxila", null },
                    { 1010, 104, "Multan City", null },
                    { 1011, 104, "Shujabad", null },
                    { 1012, 104, "Jalalpur Pirwala", null },
                    { 1013, 105, "Sahiwal", null },
                    { 1014, 105, "Chichawatni", null },
                    { 1015, 106, "Sialkot", null },
                    { 1016, 106, "Daska", null },
                    { 1017, 106, "Pasrur", null },
                    { 1018, 107, "Gujranwala City", null },
                    { 1019, 107, "Kamoke", null },
                    { 1020, 107, "Nowshera Virkan", null },
                    { 1021, 108, "Bahawalpur City", null },
                    { 1022, 108, "Yazman", null },
                    { 1023, 108, "Ahmedpur East", null },
                    { 1024, 109, "Jhang", null },
                    { 1025, 109, "Chiniot", null },
                    { 1026, 109, "Shorkot", null },
                    { 1027, 110, "Okara", null },
                    { 1028, 110, "Depalpur", null },
                    { 2001, 201, "Saddar", null },
                    { 2002, 201, "Clifton", null },
                    { 2003, 202, "Hyderabad City", null },
                    { 2004, 202, "Latifabad", null },
                    { 2005, 202, "Qasimabad", null },
                    { 2006, 203, "Sukkur City", null },
                    { 2007, 203, "Rohri", null },
                    { 2008, 204, "Larkana City", null },
                    { 2009, 204, "Ratodero", null },
                    { 2010, 205, "Nawabshah", null },
                    { 2011, 205, "Daur", null },
                    { 2012, 206, "Mirpur Khas", null },
                    { 2013, 206, "Digri", null },
                    { 2014, 207, "Thatta", null },
                    { 2015, 207, "Mirpur Sakro", null },
                    { 2016, 208, "Badin", null },
                    { 2017, 208, "Tando Bago", null },
                    { 3001, 301, "Peshawar City", null },
                    { 3002, 301, "Peshawar Saddar", null },
                    { 3003, 301, "Charsadda Road", null },
                    { 3004, 302, "Mardan", null },
                    { 3005, 302, "Takht-i-Bahi", null },
                    { 3006, 303, "Mingora", null },
                    { 3007, 303, "Kabal", null },
                    { 3008, 303, "Barikot", null },
                    { 3009, 304, "Abbottabad", null },
                    { 3010, 304, "Havelian", null },
                    { 3011, 305, "Dera Ismail Khan", null },
                    { 3012, 305, "Kulachi", null },
                    { 3013, 306, "Mansehra", null },
                    { 3014, 306, "Balakot", null },
                    { 4001, 401, "Quetta City", null },
                    { 4002, 401, "Quetta Saddar", null },
                    { 4003, 402, "Gwadar", null },
                    { 4004, 402, "Pasni", null },
                    { 4005, 403, "Khuzdar", null },
                    { 4006, 403, "Wadh", null },
                    { 4007, 404, "Sibi", null },
                    { 4008, 405, "Turbat", null },
                    { 5001, 501, "Islamabad", null },
                    { 6001, 601, "Gilgit", null },
                    { 6002, 602, "Skardu", null },
                    { 7001, 701, "Muzaffarabad", null },
                    { 7002, 702, "Mirpur", null }
                });
        }
    }
}
