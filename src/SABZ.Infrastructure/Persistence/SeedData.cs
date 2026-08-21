using Microsoft.EntityFrameworkCore;
using SABZ.Domain.Entities;

namespace SABZ.Infrastructure.Persistence;

/// <summary>
/// Seed data for crop catalog, crop requirements and regional crop suitability.
///
/// Administrative data (Province, District, Tehsil) is now seeded at runtime by
/// <see cref="LocationDataSeeder"/> from an embedded JSON resource.
///
/// Crop catalog sources: FAO (Food and Agriculture Organization) crop databases and
/// Pakistan Agricultural Research Council (PARC) public crop profiles.
///
/// Crop requirements and regional suitability data represent general agronomic
/// knowledge and are NOT prescriptive advice. Expert review is recommended before
/// using them for production decisions.
/// </summary>
public static class SeedData
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        SeedCropCatalog(modelBuilder);
        SeedCropRequirements(modelBuilder);
        SeedRegionalSuitability(modelBuilder);
        SeedCropChangeRules(modelBuilder);
        SeedDiseaseInformation(modelBuilder);
        SeedCropMonitoringRules(modelBuilder);
    }

    // Province, District, and Tehsil seeding has been moved to LocationDataSeeder (runtime JSON-based).
    // See: LocationDataSeeder.cs and SeedData/pakistan-admin-data.json

    private static void SeedCropCatalog(ModelBuilder mb)
    {
        // Source: FAO crop profiles and Pakistan Agricultural Research Council (PARC) public data.
        mb.Entity<CropCatalog>().HasData(
            new { Id = 1, Name = "Wheat", ScientificName = "Triticum aestivum", Category = "Cereal", Description = "Staple Rabi cereal crop, primary food grain of Pakistan." },
            new { Id = 2, Name = "Rice", ScientificName = "Oryza sativa", Category = "Cereal", Description = "Major Kharif cereal, key export crop especially Basmati varieties." },
            new { Id = 3, Name = "Cotton", ScientificName = "Gossypium hirsutum", Category = "Fiber", Description = "Primary fiber crop and backbone of Pakistan's textile industry." },
            new { Id = 4, Name = "Sugarcane", ScientificName = "Saccharum officinarum", Category = "Cash Crop", Description = "Major Kharif cash crop for sugar production." },
            new { Id = 5, Name = "Maize", ScientificName = "Zea mays", Category = "Cereal", Description = "Versatile cereal used for food, feed, and industry." },
            new { Id = 6, Name = "Potato", ScientificName = "Solanum tuberosum", Category = "Vegetable", Description = "Important Rabi vegetable crop." },
            new { Id = 7, Name = "Tomato", ScientificName = "Solanum lycopersicum", Category = "Vegetable", Description = "Widely cultivated vegetable across all provinces." },
            new { Id = 8, Name = "Onion", ScientificName = "Allium cepa", Category = "Vegetable", Description = "Essential vegetable crop grown in both Rabi and Kharif seasons." },
            new { Id = 9, Name = "Chili Pepper", ScientificName = "Capsicum annuum", Category = "Spice", Description = "Major spice crop, Sindh is the largest producer." },
            new { Id = 10, Name = "Mango", ScientificName = "Mangifera indica", Category = "Fruit", Description = "Premium fruit crop, Multan and Sindh are major growing areas." },
            new { Id = 11, Name = "Citrus", ScientificName = "Citrus reticulata", Category = "Fruit", Description = "Kinnow mandarin is a major export fruit from Punjab." },
            new { Id = 12, Name = "Gram (Chickpea)", ScientificName = "Cicer arietinum", Category = "Pulse", Description = "Important Rabi pulse crop for protein." },
            new { Id = 13, Name = "Lentil", ScientificName = "Lens culinaris", Category = "Pulse", Description = "Rabi pulse widely grown in rainfed areas." },
            new { Id = 14, Name = "Mustard", ScientificName = "Brassica campestris", Category = "Oilseed", Description = "Rabi oilseed crop." },
            new { Id = 15, Name = "Sunflower", ScientificName = "Helianthus annuus", Category = "Oilseed", Description = "Oilseed crop suitable for both Rabi and Kharif." },
            new { Id = 16, Name = "Groundnut", ScientificName = "Arachis hypogaea", Category = "Oilseed", Description = "Kharif oilseed crop, primarily grown in Punjab and KP." },
            new { Id = 17, Name = "Tobacco", ScientificName = "Nicotiana tabacum", Category = "Cash Crop", Description = "Cash crop primarily grown in KP." },
            new { Id = 18, Name = "Date Palm", ScientificName = "Phoenix dactylifera", Category = "Fruit", Description = "Important fruit crop of Sindh and Balochistan." },
            new { Id = 19, Name = "Apple", ScientificName = "Malus domestica", Category = "Fruit", Description = "Major fruit crop of Balochistan and northern areas." },
            new { Id = 20, Name = "Barley", ScientificName = "Hordeum vulgare", Category = "Cereal", Description = "Rabi cereal, used for animal feed and food." },
            new { Id = 21, Name = "Mung bean", ScientificName = "Vigna radiata", Category = "Pulse", Description = "Short-duration Kharif pulse, popular as catch crop and soil improver." },
            new { Id = 22, Name = "Mash bean", ScientificName = "Vigna mungo", Category = "Pulse", Description = "Heat-tolerant Kharif pulse grown in Punjab and Sindh." }
        );
    }

    private static void SeedCropRequirements(ModelBuilder mb)
    {
        // Source: Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended).
        // SuitableSoils uses a simple CSV foundation format; values match the free-text farm SoilType.
        const string source = "Initial SABZ suitability dataset (general agronomic knowledge, expert review recommended)";

        mb.Entity<CropRequirement>().HasData(
            new { Id = 1, CropCatalogId = 1, Season = "Rabi", GrowingDurationDays = (int?)150, MinTempC = (decimal?)3, MaxTempC = (decimal?)25, WaterRequirement = "Medium", SuitableSoils = "Loam,Loamy,Clay Loam,Sandy Loam,Alluvial", Source = source },
            new { Id = 2, CropCatalogId = 2, Season = "Kharif", GrowingDurationDays = (int?)130, MinTempC = (decimal?)20, MaxTempC = (decimal?)37, WaterRequirement = "High", SuitableSoils = "Clay,Clay Loam,Alluvial,Loam,Loamy", Source = source },
            new { Id = 3, CropCatalogId = 5, Season = "Kharif", GrowingDurationDays = (int?)110, MinTempC = (decimal?)15, MaxTempC = (decimal?)35, WaterRequirement = "Medium", SuitableSoils = "Loam,Loamy,Sandy Loam,Well-Drained", Source = source },
            new { Id = 4, CropCatalogId = 3, Season = "Kharif", GrowingDurationDays = (int?)160, MinTempC = (decimal?)20, MaxTempC = (decimal?)40, WaterRequirement = "High", SuitableSoils = "Loam,Loamy,Sandy Loam,Alluvial", Source = source },
            new { Id = 5, CropCatalogId = 4, Season = "Kharif", GrowingDurationDays = (int?)330, MinTempC = (decimal?)20, MaxTempC = (decimal?)38, WaterRequirement = "High", SuitableSoils = "Loam,Loamy,Clay Loam,Alluvial", Source = source },
            new { Id = 6, CropCatalogId = 12, Season = "Rabi", GrowingDurationDays = (int?)110, MinTempC = (decimal?)5, MaxTempC = (decimal?)28, WaterRequirement = "Low", SuitableSoils = "Loam,Loamy,Sandy Loam,Clay Loam", Source = source },
            new { Id = 7, CropCatalogId = 13, Season = "Rabi", GrowingDurationDays = (int?)120, MinTempC = (decimal?)4, MaxTempC = (decimal?)27, WaterRequirement = "Low", SuitableSoils = "Loam,Loamy,Sandy Loam,Clay Loam", Source = source },
            new { Id = 8, CropCatalogId = 21, Season = "Kharif", GrowingDurationDays = (int?)70, MinTempC = (decimal?)20, MaxTempC = (decimal?)38, WaterRequirement = "Low", SuitableSoils = "Loam,Loamy,Sandy Loam", Source = source },
            new { Id = 9, CropCatalogId = 22, Season = "Kharif", GrowingDurationDays = (int?)80, MinTempC = (decimal?)20, MaxTempC = (decimal?)40, WaterRequirement = "Low", SuitableSoils = "Sandy Loam,Loam,Loamy", Source = source }
        );
    }

    private static void SeedRegionalSuitability(ModelBuilder mb)
    {
        // Source: Pakistan Agricultural Research Council (PARC) crop-zone publications
        // and FAO country programming framework for Pakistan.
        // Scores: 1-3 Low, 4-6 Moderate, 7-8 High, 9-10 Very High.
        // Precedence at evaluation time: tehsil rule > district rule > province rule (DistrictId null).
        // District IDs reference the Pakistan Administrative Divisions dataset
        // (Punjab=1, Balochistan=3, KP=6, Sindh=7).
        // This is general agronomic knowledge, not prescriptive recommendations.
        const string source = "PARC/FAO crop-zone data (general agronomic knowledge, not prescriptive advice)";

        mb.Entity<RegionalCropSuitability>().HasData(
            // ---------------- Punjab district rules ----------------
            new { Id = 1, ProvinceId = 1, DistrictId = (int?)102, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 9, SuitabilityLevel = "Very High", Notes = "Faisalabad is in the heart of Punjab's wheat belt with fertile alluvial soil.", Source = source },
            new { Id = 2, ProvinceId = 1, DistrictId = (int?)105, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 9, SuitabilityLevel = "Very High", Notes = "Sahiwal division is a major wheat producing region.", Source = source },
            new { Id = 3, ProvinceId = 1, DistrictId = (int?)104, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Southern Punjab wheat with irrigation support.", Source = source },
            new { Id = 4, ProvinceId = 1, DistrictId = (int?)101, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Lahore district grows wheat on irrigated alluvial soils.", Source = source },
            new { Id = 5, ProvinceId = 1, DistrictId = (int?)103, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Rawalpindi grows wheat in the Potohar rainfed/irrigated mix.", Source = source },

            new { Id = 6, ProvinceId = 1, DistrictId = (int?)106, TehsilId = (int?)null, CropCatalogId = 2, Season = "Kharif", SuitabilityScore = 9, SuitabilityLevel = "Very High", Notes = "Sialkot-Gujranwala belt is famous for Basmati rice.", Source = source },
            new { Id = 7, ProvinceId = 1, DistrictId = (int?)107, TehsilId = (int?)null, CropCatalogId = 2, Season = "Kharif", SuitabilityScore = 9, SuitabilityLevel = "Very High", Notes = "Gujranwala is a major rice growing district.", Source = source },

            new { Id = 8, ProvinceId = 1, DistrictId = (int?)104, TehsilId = (int?)null, CropCatalogId = 3, Season = "Kharif", SuitabilityScore = 9, SuitabilityLevel = "Very High", Notes = "Multan is in Pakistan's cotton belt.", Source = source },
            new { Id = 9, ProvinceId = 1, DistrictId = (int?)108, TehsilId = (int?)null, CropCatalogId = 3, Season = "Kharif", SuitabilityScore = 9, SuitabilityLevel = "Very High", Notes = "Bahawalpur division is a major cotton area.", Source = source },
            new { Id = 10, ProvinceId = 1, DistrictId = (int?)105, TehsilId = (int?)null, CropCatalogId = 3, Season = "Kharif", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Sahiwal has good cotton suitability.", Source = source },

            new { Id = 11, ProvinceId = 1, DistrictId = (int?)102, TehsilId = (int?)null, CropCatalogId = 4, Season = "Kharif", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Faisalabad region has multiple sugar mills nearby.", Source = source },
            new { Id = 12, ProvinceId = 1, DistrictId = (int?)109, TehsilId = (int?)null, CropCatalogId = 4, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Jhang has good sugarcane growing conditions.", Source = source },

            new { Id = 13, ProvinceId = 1, DistrictId = (int?)103, TehsilId = (int?)null, CropCatalogId = 12, Season = "Rabi", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Potohar tract (Rawalpindi) is a traditional gram growing area.", Source = source },
            new { Id = 14, ProvinceId = 1, DistrictId = (int?)109, TehsilId = (int?)null, CropCatalogId = 12, Season = "Rabi", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Jhang supports gram on lighter soils.", Source = source },
            new { Id = 15, ProvinceId = 1, DistrictId = (int?)103, TehsilId = (int?)null, CropCatalogId = 13, Season = "Rabi", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Rainfed Potohar areas grow lentil (masoor).", Source = source },

            new { Id = 16, ProvinceId = 1, DistrictId = (int?)108, TehsilId = (int?)null, CropCatalogId = 21, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Southern Punjab grows mung bean as a short Kharif pulse.", Source = source },
            new { Id = 17, ProvinceId = 1, DistrictId = (int?)104, TehsilId = (int?)null, CropCatalogId = 22, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Multan region supports heat-tolerant mash bean.", Source = source },

            // ---------------- Sindh district rules ----------------
            new { Id = 18, ProvinceId = 7, DistrictId = (int?)243, TehsilId = (int?)null, CropCatalogId = 2, Season = "Kharif", SuitabilityScore = 9, SuitabilityLevel = "Very High", Notes = "Larkana is famous for Sindh rice varieties.", Source = source },
            new { Id = 19, ProvinceId = 7, DistrictId = (int?)254, TehsilId = (int?)null, CropCatalogId = 2, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Sukkur barrage supports rice cultivation.", Source = source },
            new { Id = 20, ProvinceId = 7, DistrictId = (int?)250, TehsilId = (int?)null, CropCatalogId = 3, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Shaheed Benazir Abad (Nawabshah) area supports cotton growing.", Source = source },
            new { Id = 21, ProvinceId = 7, DistrictId = (int?)232, TehsilId = (int?)null, CropCatalogId = 4, Season = "Kharif", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Badin has sugar mills and sugarcane farms.", Source = source },
            new { Id = 22, ProvinceId = 7, DistrictId = (int?)232, TehsilId = (int?)null, CropCatalogId = 21, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Badin grows mung bean on coastal plain soils.", Source = source },

            // ---------------- Khyber Pakhtunkhwa district rules ----------------
            new { Id = 23, ProvinceId = 6, DistrictId = (int?)228, TehsilId = (int?)null, CropCatalogId = 5, Season = "Kharif", SuitabilityScore = 9, SuitabilityLevel = "Very High", Notes = "Swat valley is a major maize growing area.", Source = source },
            new { Id = 24, ProvinceId = 6, DistrictId = (int?)219, TehsilId = (int?)null, CropCatalogId = 5, Season = "Kharif", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Mardan supports maize cultivation.", Source = source },
            new { Id = 25, ProvinceId = 6, DistrictId = (int?)224, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Peshawar valley supports wheat cultivation.", Source = source },
            new { Id = 26, ProvinceId = 6, DistrictId = (int?)219, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Mardan has good wheat growing conditions.", Source = source },

            // ---------------- Balochistan district rules ----------------
            new { Id = 27, ProvinceId = 3, DistrictId = (int?)177, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 5, SuitabilityLevel = "Moderate", Notes = "Sibi has moderate wheat suitability with irrigation.", Source = source },

            // ---------------- Province-level baseline rules ----------------
            // DistrictId null = province baseline (lowest precedence).
            new { Id = 28, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Punjab is Pakistan's largest wheat producing province.", Source = source },
            new { Id = 29, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 2, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Punjab grows rice widely in the central and northeast belts.", Source = source },
            new { Id = 30, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 5, Season = "Kharif", SuitabilityScore = 6, SuitabilityLevel = "Moderate", Notes = "Punjab grows maize but KP is the leading province.", Source = source },
            new { Id = 31, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 3, Season = "Kharif", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Southern and central Punjab form the national cotton belt.", Source = source },
            new { Id = 32, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 4, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Punjab is the main sugarcane producing province.", Source = source },
            new { Id = 33, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 12, Season = "Rabi", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Punjab's rainfed tracts are traditional gram areas.", Source = source },
            new { Id = 34, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 13, Season = "Rabi", SuitabilityScore = 6, SuitabilityLevel = "Moderate", Notes = "Lentil is grown in northern Punjab rainfed areas.", Source = source },
            new { Id = 35, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 21, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Punjab is the main mung bean producing province.", Source = source },
            new { Id = 36, ProvinceId = 1, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 22, Season = "Kharif", SuitabilityScore = 6, SuitabilityLevel = "Moderate", Notes = "Mash bean is grown in southern Punjab.", Source = source },

            new { Id = 37, ProvinceId = 7, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 2, Season = "Kharif", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "Sindh grows rice extensively along the Indus.", Source = source },
            new { Id = 38, ProvinceId = 7, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 3, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Sindh is a major cotton producing province.", Source = source },
            new { Id = 39, ProvinceId = 7, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 4, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Sindh supports sugarcane near sugar mills.", Source = source },
            new { Id = 40, ProvinceId = 7, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 21, Season = "Kharif", SuitabilityScore = 7, SuitabilityLevel = "High", Notes = "Sindh grows mung bean on lighter soils.", Source = source },

            new { Id = 41, ProvinceId = 6, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 5, Season = "Kharif", SuitabilityScore = 8, SuitabilityLevel = "High", Notes = "KP is Pakistan's leading maize province.", Source = source },
            new { Id = 42, ProvinceId = 6, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 6, SuitabilityLevel = "Moderate", Notes = "KP grows wheat in the Peshawar and southern valleys.", Source = source },

            new { Id = 43, ProvinceId = 3, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 1, Season = "Rabi", SuitabilityScore = 5, SuitabilityLevel = "Moderate", Notes = "Balochistan grows wheat in irrigated highland areas.", Source = source },
            new { Id = 44, ProvinceId = 3, DistrictId = (int?)null, TehsilId = (int?)null, CropCatalogId = 12, Season = "Rabi", SuitabilityScore = 5, SuitabilityLevel = "Moderate", Notes = "Balochistan highlands support gram under rainfall.", Source = source }
        );
    }

    private static void SeedCropChangeRules(ModelBuilder mb)
    {
        // Source: Initial SABZ crop-change reference dataset (general agronomic knowledge).
        // Category-level rules keyed by CropCatalog.Category. This is a small, clearly
        // labelled foundation dataset - NOT a complete scientific rotation model.
        // Extend with expert-reviewed / official agricultural sources in future phases.
        const string source = "Initial SABZ crop-change reference dataset (general agronomic knowledge, expert review recommended)";

        mb.Entity<CropChangeRule>().HasData(
            new { Id = 1, PreviousCategory = "Pulse", NextCategory = "Cereal", Effect = "Positive", Explanation = "Pulses are generally considered to leave residual soil nitrogen, which commonly benefits a following cereal crop.", IsActive = true, Source = source },
            new { Id = 2, PreviousCategory = "Cereal", NextCategory = "Pulse", Effect = "Positive", Explanation = "Alternating cereals with pulses is widely considered sound rotation practice; pulses help maintain soil nitrogen.", IsActive = true, Source = source },
            new { Id = 3, PreviousCategory = "Cereal", NextCategory = "Cereal", Effect = "Caution", Explanation = "Repeated cereal cropping can build up similar pests/diseases and draw on the same nutrients; rotating with a different crop group is commonly advised.", IsActive = true, Source = source },
            new { Id = 4, PreviousCategory = "Vegetable", NextCategory = "Vegetable", Effect = "Caution", Explanation = "Growing vegetables back-to-back can increase pest and disease carry-over; rotating with a different crop group is commonly advised.", IsActive = true, Source = source },
            new { Id = 5, PreviousCategory = "Pulse", NextCategory = "Pulse", Effect = "Caution", Explanation = "Consecutive pulse crops can build up pulse-specific diseases; alternating with another crop group is commonly advised.", IsActive = true, Source = source },
            new { Id = 6, PreviousCategory = "Oilseed", NextCategory = "Cereal", Effect = "Positive", Explanation = "Oilseeds are generally considered a good preceding crop for cereals due to different rooting and nutrient use.", IsActive = true, Source = source }
        );
    }

    private static void SeedDiseaseInformation(ModelBuilder mb)
    {
        // Source: Initial SABZ disease reference dataset (general plant-health knowledge).
        // Small, clearly labelled foundation dataset - NOT authoritative treatment guidance.
        // No chemical dosages are stored by design; farmers are directed to approved product
        // labels and local agricultural experts. Extend with expert-reviewed data later.
        // List fields use semicolon-separated values.
        const string source = "Initial SABZ disease reference dataset (general plant-health knowledge, expert review recommended)";

        mb.Entity<DiseaseInformation>().HasData(
            new
            {
                Id = 1,
                DiseaseName = "Wheat Leaf Rust",
                CropCatalogId = (int?)1,
                Description = "A common fungal disease of wheat that appears as small round brown-orange pustules scattered on leaf surfaces and can reduce grain filling when severe.",
                Symptoms = "Small round orange-brown pustules on leaves; yellowing around pustules; premature leaf drying in severe cases.",
                RecommendedActions = "If only a few leaves are affected, remove and destroy them away from the field; if spreading, consult the local agricultural extension office promptly; avoid late-season excess nitrogen which can worsen rust",
                Prevention = "Grow rust-resistant varieties where available; avoid very dense stands; monitor fields regularly during humid weather",
                Monitoring = "Check lower and middle leaves twice weekly during tillering to grain fill; record whether pustules are increasing or spreading to new leaves",
                Source = source,
                IsActive = true
            },
            new
            {
                Id = 2,
                DiseaseName = "Rice Blast",
                CropCatalogId = (int?)2,
                Description = "A major fungal disease of rice causing diamond-shaped lesions on leaves and can affect necks of panicles, especially under warm humid conditions.",
                Symptoms = "Diamond/eye-shaped grey lesions with brown borders on leaves; lesions on nodes; neck rot in severe cases.",
                RecommendedActions = "Avoid excess nitrogen application; maintain balanced water management; consult the local agricultural extension office if lesions are spreading",
                Prevention = "Use certified seed and resistant varieties; avoid prolonged leaf wetness; keep balanced fertility",
                Monitoring = "Inspect leaves weekly during warm humid periods; watch for new diamond-shaped lesions and neck symptoms near flowering",
                Source = source,
                IsActive = true
            },
            new
            {
                Id = 3,
                DiseaseName = "Tomato Early Blight",
                CropCatalogId = (int?)7,
                Description = "A common fungal leaf disease of tomato showing dark concentric target-like spots, usually starting on older lower leaves.",
                Symptoms = "Dark brown spots with concentric rings (target-like) on older leaves; yellow halo around spots; lower leaves drying first.",
                RecommendedActions = "Remove affected lower leaves and dispose away from the field; improve air circulation; avoid wetting leaves when irrigating",
                Prevention = "Mulch soil to reduce splash; rotate away from tomato/potato; water at the base of plants in the morning",
                Monitoring = "Check lower leaves twice weekly, especially after rain; note whether spots are moving upward on the plant",
                Source = source,
                IsActive = true
            },
            new
            {
                Id = 4,
                DiseaseName = "Tomato Leaf Curl Virus",
                CropCatalogId = (int?)7,
                Description = "A virus disease spread by whiteflies causing upward curling, crinkling and yellowing of tomato leaves and reduced fruit set.",
                Symptoms = "Upward curling and crinkling of leaves; yellowing; stunted growth; poor fruit setting.",
                RecommendedActions = "Remove clearly affected plants to reduce spread; control whitefly populations with guidance from a local expert; avoid moving plant material between fields",
                Prevention = "Use healthy transplants; monitor and manage whiteflies early; consider reflective mulches where practical",
                Monitoring = "Look weekly for new curling or yellowing plants and for whiteflies on leaf undersides",
                Source = source,
                IsActive = true
            },
            new
            {
                Id = 5,
                DiseaseName = "Potato Late Blight",
                CropCatalogId = (int?)6,
                Description = "A serious disease of potato favoured by cool wet weather, causing water-soaked dark lesions on leaves that can spread rapidly through a field.",
                Symptoms = "Water-soaked dark patches on leaf tips and edges; white mould under leaves in humid weather; rapid browning and collapse.",
                RecommendedActions = "Act quickly - remove and destroy affected foliage if limited; seek expert advice immediately if spreading, as late blight can escalate within days",
                Prevention = "Use certified seed; avoid overhead irrigation late in the day; ensure good spacing for airflow",
                Monitoring = "Inspect fields every 2-3 days during cool wet spells; check leaf undersides for white mould",
                Source = source,
                IsActive = true
            },
            new
            {
                Id = 6,
                DiseaseName = "Cotton Leaf Curl Virus",
                CropCatalogId = (int?)3,
                Description = "A virus disease of cotton spread by whiteflies, causing leaf curling, vein thickening and enations, and significant yield loss in susceptible varieties.",
                Symptoms = "Upward/downward curling of leaves; thickened veins; leaf-like outgrowths (enations) on leaf undersides; stunted plants.",
                RecommendedActions = "Remove clearly affected plants early; manage whiteflies following local expert guidance; avoid late sowing where the disease is known to be common",
                Prevention = "Use tolerant varieties where available; manage whitefly populations early; keep fields free of alternate hosts",
                Monitoring = "Check young plants weekly for curling and enations; monitor whitefly numbers on leaf undersides",
                Source = source,
                IsActive = true
            }
        );
    }

    private static void SeedCropMonitoringRules(ModelBuilder mb)
    {
        // Source: Initial SABZ monitoring reference dataset (general agronomic knowledge).
        // A small, honestly labelled foundation dataset of scheduled monitoring checkpoints
        // for major crops - NOT scientifically exhaustive. Extend with expert-reviewed
        // guidance later. InspectionItems use semicolon-separated values.
        // TriggerType is "Scheduled" (days after planting); future trigger kinds such as
        // WeatherEvent, SatelliteAlert or Manual can be added as new reference rows.
        const string source = "Initial SABZ monitoring reference dataset (general agronomic knowledge, expert review recommended)";

        mb.Entity<CropMonitoringRule>().HasData(
            // ---------------- Wheat (CropCatalogId 1) ----------------
            new { Id = 1, CropCatalogId = (int?)1, DayOffsetAfterPlanting = 14, Title = "Early growth and emergence check", Description = "Check that seedlings have emerged evenly and look for early signs of stress or damage in several places across the field.", InspectionItems = "Even seedling emergence; yellowing of young leaves; unusual spots; insect feeding on seedlings; weed seedlings", Priority = "Medium", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 2, CropCatalogId = (int?)1, DayOffsetAfterPlanting = 30, Title = "Leaf health and pest check", Description = "Examine leaves on several plants for spots, discoloration and insect damage, and check how weeds are competing with the crop.", InspectionItems = "Leaf spots; yellowing or rust-coloured pustules; holes or insect damage; weed competition; stunted plants", Priority = "High", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 3, CropCatalogId = (int?)1, DayOffsetAfterPlanting = 60, Title = "Mid-season disease and crop health check", Description = "Walk the field and inspect the middle and upper leaves for disease symptoms and overall crop condition.", InspectionItems = "Rust pustules or stripes; powdery patches; pest damage; wilting or weak stems; general crop vigour", Priority = "Medium", TriggerType = "Scheduled", IsActive = true, Source = source },

            // ---------------- Rice (CropCatalogId 2) ----------------
            new { Id = 4, CropCatalogId = (int?)2, DayOffsetAfterPlanting = 15, Title = "Establishment check", Description = "Check seedling establishment and look for early problems in the paddy.", InspectionItems = "Even stand; missing or dead seedlings; yellowing; snails or insects; weed growth", Priority = "Medium", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 5, CropCatalogId = (int?)2, DayOffsetAfterPlanting = 35, Title = "Leaf disease and pest check", Description = "Inspect leaves on several hills for lesions and insects, especially during warm humid weather.", InspectionItems = "Diamond-shaped or oval leaf lesions; yellowing; leaf folders or stem borers; brown planthopper insects at the base", Priority = "High", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 6, CropCatalogId = (int?)2, DayOffsetAfterPlanting = 70, Title = "Flowering-stage health check", Description = "Check the crop around flowering for diseases and grain development problems.", InspectionItems = "Neck or node lesions; discoloured grains; pest damage; uneven flowering; weed escapes", Priority = "Medium", TriggerType = "Scheduled", IsActive = true, Source = source },

            // ---------------- Cotton (CropCatalogId 3) ----------------
            new { Id = 7, CropCatalogId = (int?)3, DayOffsetAfterPlanting = 20, Title = "Seedling and leaf curl check", Description = "Inspect young plants for vigour and early leaf-curl symptoms spread by whiteflies.", InspectionItems = "Curling or crinkled leaves; thickened veins; whiteflies on leaf undersides; missing plants; insect damage", Priority = "High", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 8, CropCatalogId = (int?)3, DayOffsetAfterPlanting = 45, Title = "Pest pressure check", Description = "Check leaves, buds and squares for bollworms, aphids, whiteflies and mites.", InspectionItems = "Holes in buds or squares; sticky honeydew; curled or bronzed leaves; pest eggs or larvae on undersides", Priority = "High", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 9, CropCatalogId = (int?)3, DayOffsetAfterPlanting = 90, Title = "Mid-season boll health check", Description = "Inspect developing bolls and upper leaves for pests, disease symptoms and plant health.", InspectionItems = "Damaged or dropped bolls; leaf spots; wilting branches; pest activity; overall plant vigour", Priority = "Medium", TriggerType = "Scheduled", IsActive = true, Source = source },

            // ---------------- Potato (CropCatalogId 6) ----------------
            new { Id = 10, CropCatalogId = (int?)6, DayOffsetAfterPlanting = 14, Title = "Emergence check", Description = "Check that plants have emerged evenly and look for early leaf problems.", InspectionItems = "Even emergence; missing plants; dark or water-soaked leaf patches; insect feeding; weed seedlings", Priority = "Medium", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 11, CropCatalogId = (int?)6, DayOffsetAfterPlanting = 30, Title = "Blight and pest check", Description = "Inspect leaf tips and undersides during cool wet weather for early blight symptoms and pests.", InspectionItems = "Water-soaked dark patches; white mould under leaves; holes or larvae; yellowing; weed competition", Priority = "High", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 12, CropCatalogId = (int?)6, DayOffsetAfterPlanting = 55, Title = "Tuber-stage health check", Description = "Check foliage health as tubers develop; late blight can escalate within days, so inspect carefully.", InspectionItems = "Rapid browning or collapse; spreading lesions; pest damage; wilting; overall canopy condition", Priority = "High", TriggerType = "Scheduled", IsActive = true, Source = source },

            // ---------------- Tomato (CropCatalogId 7) ----------------
            new { Id = 13, CropCatalogId = (int?)7, DayOffsetAfterPlanting = 14, Title = "Transplant establishment check", Description = "Check transplants for establishment and early leaf problems.", InspectionItems = "Wilting or dead transplants; dark spots on lower leaves; curling; cutworm damage; weed seedlings", Priority = "Medium", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 14, CropCatalogId = (int?)7, DayOffsetAfterPlanting = 30, Title = "Leaf spot and pest check", Description = "Examine leaves, especially the lower ones, for target-like spots and pests.", InspectionItems = "Dark concentric spots; yellowing around spots; holes from caterpillars; aphids or whiteflies; curling leaves", Priority = "High", TriggerType = "Scheduled", IsActive = true, Source = source },
            new { Id = 15, CropCatalogId = (int?)7, DayOffsetAfterPlanting = 60, Title = "Fruiting-stage health check", Description = "Inspect foliage and developing fruit during fruiting for disease and damage.", InspectionItems = "Spots moving up the plant; fruit blemishes; wilting; leaf mould under humid conditions; pest damage on fruit", Priority = "Medium", TriggerType = "Scheduled", IsActive = true, Source = source }
        );
    }
}
