using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SABZ.Domain.Entities;

namespace SABZ.Infrastructure.Persistence;

/// <summary>
/// Idempotent runtime seeder for Pakistan administrative data (Province, District, Tehsil).
/// Reads from an embedded JSON resource generated from the open dataset:
///   Pakistan Administrative Divisions – https://github.com/open-admin-data/pakistan-administrative-divisions
///   License: CC-BY-4.0 | Updated: June 1, 2026
///
/// This replaces the EF Core HasData approach for location tables so that existing
/// rows (referenced by Farm FKs) are never deleted when the dataset is updated.
/// </summary>
public static class LocationDataSeeder
{
    private const string ResourceName = "SABZ.Infrastructure.SeedData.pakistan-admin-data.json";

    public static async Task SeedAsync(SabzDbContext db, ILogger logger)
    {
        var data = LoadEmbeddedData();
        if (data is null)
        {
            logger.LogWarning("Embedded seed resource '{Resource}' not found – skipping location seed.", ResourceName);
            return;
        }

        var inserted = await SeedProvincesAsync(db, data.Provinces);
        inserted += await SeedDistrictsAsync(db, data.Districts);
        inserted += await SeedTehsilsAsync(db, data.Tehsils);

        // One-time backfill: set Tehsil coordinates from district centres.
        var updated = await BackfillTehsilCoordinatesAsync(db);

        // One-time cleanup: remove duplicate tehsils that were inserted before
        // name-based deduplication was added (old seed IDs vs new dataset IDs).
        var removed = await CleanupDuplicateTehsilsAsync(db);

        if (inserted > 0 || removed > 0 || updated > 0)
        {
            logger.LogInformation("Location seed: inserted {Inserted} new records, updated {Updated} tehsil coordinates, removed {Removed} duplicate tehsils.", inserted, updated, removed);
        }
        else
        {
            logger.LogInformation("Location seed: all records already present – no changes.");
        }
    }

    // ------------------------------------------------------------------
    //  Provinces
    // ------------------------------------------------------------------
    private static async Task<int> SeedProvincesAsync(SabzDbContext db, List<SeedProvince> provinces)
    {
        var existingIds = await db.Provinces.Select(p => p.Id).ToListAsync();
        var toInsert = provinces.Where(p => !existingIds.Contains(p.Id)).ToList();
        if (toInsert.Count == 0) return 0;

        await InsertWithIdentityAsync(db, "Provinces", () =>
        {
            foreach (var p in toInsert)
            {
                db.Provinces.Add(new Province
                {
                    Id = p.Id,
                    Name = p.Name,
                    NameUrdu = p.NameUrdu
                });
            }
        });
        return toInsert.Count;
    }

    // ------------------------------------------------------------------
    //  Districts
    // ------------------------------------------------------------------
    private static async Task<int> SeedDistrictsAsync(SabzDbContext db, List<SeedDistrict> districts)
    {
        var existingIds = await db.Districts.Select(d => d.Id).ToListAsync();
        var toInsert = districts.Where(d => !existingIds.Contains(d.Id)).ToList();
        if (toInsert.Count == 0) return 0;

        await InsertWithIdentityAsync(db, "Districts", () =>
        {
            foreach (var d in toInsert)
            {
                db.Districts.Add(new District
                {
                    Id = d.Id,
                    ProvinceId = d.ProvinceId,
                    Name = d.Name,
                    NameUrdu = d.NameUrdu
                });
            }
        });
        return toInsert.Count;
    }

    // ------------------------------------------------------------------
    //  Tehsils
    // ------------------------------------------------------------------
    private static async Task<int> SeedTehsilsAsync(SabzDbContext db, List<SeedTehsil> tehsils)
    {
        var existingIds = await db.Tehsils.Select(t => t.Id).ToListAsync();
        var existingNameDistrict = await db.Tehsils
            .Select(t => new { t.Name, t.DistrictId })
            .ToListAsync();

        var toInsert = tehsils.Where(t =>
            !existingIds.Contains(t.Id) &&
            !existingNameDistrict.Any(e => e.Name == t.Name && e.DistrictId == t.DistrictId)
        ).ToList();

        if (toInsert.Count == 0) return 0;

        await InsertWithIdentityAsync(db, "Tehsils", () =>
        {
            foreach (var t in toInsert)
            {
                db.Tehsils.Add(new Tehsil
                {
                    Id = t.Id,
                    DistrictId = t.DistrictId,
                    Name = t.Name,
                    NameUrdu = t.NameUrdu
                });
            }
        });
        return toInsert.Count;
    }

    /// <summary>
    /// One-time backfill: assigns approximate GPS coordinates to every Tehsil
    /// that does not yet have them, using the parent District centre coordinates.
    /// All tehsils within a district share the same district-centre point;
    /// this is accurate enough for weather look-ups (~10-30 km).
    /// </summary>
    private static async Task<int> BackfillTehsilCoordinatesAsync(SabzDbContext db)
    {
        var tehsilsWithoutCoords = await db.Tehsils
            .Where(t => t.Latitude == null || t.Longitude == null)
            .Include(t => t.District)
            .ToListAsync();

        if (tehsilsWithoutCoords.Count == 0) return 0;

        var updated = 0;
        foreach (var t in tehsilsWithoutCoords)
        {
            if (DistrictCoordinates.TryGetValue(t.District.Name, out var coords))
            {
                t.Latitude = coords.Lat;
                t.Longitude = coords.Lon;
                updated++;
            }
        }

        if (updated > 0) await db.SaveChangesAsync();
        return updated;
    }

    /// <summary>
    /// Removes tehsils with new dataset IDs (>= 7000) that duplicate old seed
    /// tehsils (IDs &lt; 7000) by (Name, DistrictId). Old IDs are preserved because
    /// existing Farm records may reference them.
    /// </summary>
    private static async Task<int> CleanupDuplicateTehsilsAsync(SabzDbContext db)
    {
        var allTehsils = await db.Tehsils
            .Select(t => new { t.Id, t.Name, t.DistrictId })
            .ToListAsync();

        var oldKeySet = allTehsils
            .Where(t => t.Id < 7000)
            .Select(t => (t.Name, t.DistrictId))
            .ToHashSet();

        var toDelete = allTehsils
            .Where(t => t.Id >= 7000 && oldKeySet.Contains((t.Name, t.DistrictId)))
            .ToList();

        if (toDelete.Count == 0) return 0;

        foreach (var t in toDelete)
        {
            var entity = new Tehsil { Id = t.Id, DistrictId = t.DistrictId, Name = t.Name };
            db.Tehsils.Attach(entity);
            db.Tehsils.Remove(entity);
        }

        await db.SaveChangesAsync();
        return toDelete.Count;
    }

    /// <summary>
    /// Executes inserts with IDENTITY_INSERT ON using the same database connection.
    /// </summary>
    private static async Task InsertWithIdentityAsync(SabzDbContext db, string tableName, Action addEntities)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var cmdOn = connection.CreateCommand();
        cmdOn.CommandText = $"SET IDENTITY_INSERT [{tableName}] ON";
        await cmdOn.ExecuteNonQueryAsync();

        try
        {
            addEntities();
            await db.SaveChangesAsync();
        }
        finally
        {
            using var cmdOff = connection.CreateCommand();
            cmdOff.CommandText = $"SET IDENTITY_INSERT [{tableName}] OFF";
            await cmdOff.ExecuteNonQueryAsync();
        }
    }

    // ------------------------------------------------------------------
    //  Embedded resource loader
    // ------------------------------------------------------------------
    private static SeedRoot? LoadEmbeddedData()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return null;

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return JsonSerializer.Deserialize<SeedRoot>(json, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    // ------------------------------------------------------------------
    //  Deserialization DTOs
    // ------------------------------------------------------------------
    private sealed class SeedRoot
    {
        [JsonPropertyName("source")] public SeedSource? Source { get; set; }
        [JsonPropertyName("provinces")] public List<SeedProvince> Provinces { get; set; } = new();
        [JsonPropertyName("districts")] public List<SeedDistrict> Districts { get; set; } = new();
        [JsonPropertyName("tehsils")] public List<SeedTehsil> Tehsils { get; set; } = new();
    }

    private sealed class SeedSource
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("repository")] public string Repository { get; set; } = "";
        [JsonPropertyName("license")] public string License { get; set; } = "";
        [JsonPropertyName("updated")] public string Updated { get; set; } = "";
    }

    private sealed class SeedProvince
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("nameUrdu")] public string? NameUrdu { get; set; }
    }

    private sealed class SeedDistrict
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("provinceId")] public int ProvinceId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("nameUrdu")] public string? NameUrdu { get; set; }
    }

    private sealed class SeedTehsil
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("districtId")] public int DistrictId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("nameUrdu")] public string? NameUrdu { get; set; }
    }

    // ------------------------------------------------------------------
    //  District centre coordinates (lat, lon) for weather fallback.
    //  Covers all ~150 districts of Pakistan. Tehsils inherit their
    //  parent district's centre; accurate enough for weather queries.
    //
    //  Source: GeoNames.org (CC-BY-4.0) cross-referenced with
    //  Open-Meteo Geocoding API (https://open-meteo.com) filtered to
    //  Pakistan bounding box (lat 23.5-37.1, lon 60.9-77.1).
    //  Namesakes in India, Afghanistan, etc. were manually excluded.
    // ------------------------------------------------------------------
    private static readonly Dictionary<string, (decimal Lat, decimal Lon)> DistrictCoordinates = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Punjab (36 districts) ─────────────────────────────────
        ["Attock"] = (33.7667m, 72.3598m), ["Bahawalnagar"] = (29.9984m, 73.2527m),
        ["Bahawalpur"] = (29.3978m, 71.6752m), ["Bhakkar"] = (31.6269m, 71.0647m),
        ["Chakwal"] = (32.9329m, 72.8539m), ["Chiniot"] = (31.7209m, 72.9784m),
        ["Dera Ghazi Khan"] = (30.0459m, 70.6403m), ["Faisalabad"] = (31.4155m, 73.0897m),
        ["Gujranwala"] = (32.1557m, 74.1871m), ["Gujrat"] = (32.5742m, 74.0754m),
        ["Hafizabad"] = (32.0710m, 73.6880m), ["Jhang"] = (31.3057m, 72.3258m),
        ["Jhelum"] = (32.9345m, 73.7310m), ["Kasur"] = (30.9477m, 74.3208m),
        ["Khanewal"] = (30.3017m, 71.9321m), ["Khushab"] = (32.2967m, 72.3525m),
        ["Lahore"] = (31.5580m, 74.3507m), ["Leiah"] = (30.9613m, 70.9390m),
        ["Lodhran"] = (29.5339m, 71.6324m), ["Mandi Bahauddin"] = (32.5870m, 73.4912m),
        ["Mianwali"] = (32.5776m, 71.5285m), ["Multan"] = (30.1968m, 71.4782m),
        ["Muzaffargarh"] = (30.0742m, 70.8936m), ["Nankana Sahib"] = (31.4501m, 73.7065m),
        ["Narowal"] = (32.1020m, 74.8730m), ["Okara"] = (30.8103m, 73.4516m),
        ["Pakpattan"] = (30.3431m, 73.3894m), ["Rahim Yar Khan"] = (28.4199m, 70.3035m),
        ["Rajanpur"] = (29.1041m, 70.3297m), ["Rawalpindi"] = (33.5973m, 73.0479m),
        ["Sahiwal"] = (30.6660m, 73.1019m), ["Sargodha"] = (32.0859m, 72.6742m),
        ["Sheikhupura"] = (31.7172m, 73.9780m), ["Sialkot"] = (32.4927m, 74.5313m),
        ["Toba Tek Singh"] = (30.9713m, 72.4828m), ["Vehari"] = (30.0445m, 72.3556m),

        // ── Azad Kashmir (10 districts) ──────────────────────────
        ["Bagh"] = (33.9811m, 73.7761m), ["Bhimber"] = (32.9746m, 74.0785m),
        ["Haveli"] = (33.7833m, 73.9333m), ["Jhelum Valley"] = (33.7500m, 73.9000m),
        ["Kotli"] = (33.5184m, 73.9022m), ["Mirpur"] = (33.1470m, 73.7520m),
        ["Muzaffarabad"] = (34.3700m, 73.4708m), ["Neelum"] = (34.6000m, 73.7000m),
        ["Poonch"] = (33.7703m, 74.0925m), ["Sudhnoti"] = (33.7333m, 73.8000m),

        // ── Balochistan (34 districts) ────────────────────────────
        ["Awaran"] = (26.4568m, 65.2314m), ["Barkhan"] = (29.8977m, 69.5256m),
        ["Chagai"] = (29.3539m, 64.6975m), ["Chaman"] = (30.9177m, 66.4526m),
        ["Dera Bugti"] = (29.0362m, 69.1585m), ["Duki"] = (30.1531m, 68.5732m),
        ["Gwadar"] = (25.1216m, 62.3254m), ["Harnai"] = (30.1008m, 67.9382m),
        ["Jaffarabad"] = (28.5700m, 67.9500m), ["Jhal Magsi"] = (28.2840m, 67.4562m),
        ["Kachhi"] = (29.4000m, 67.3000m), ["Kalat"] = (29.0266m, 66.5936m),
        ["Kech"] = (26.1712m, 63.0179m), ["Kharan"] = (28.5846m, 65.4150m),
        ["Khuzdar"] = (27.8119m, 66.6110m), ["Killa Abdullah"] = (30.7280m, 66.6612m),
        ["Killa Saifullah"] = (30.7000m, 68.3667m), ["Kohlu"] = (29.9030m, 69.2310m),
        ["Lasbela"] = (24.8871m, 67.0371m), ["Lehri"] = (29.1000m, 67.5000m),
        ["Loralai"] = (30.3705m, 68.5980m), ["Mastung"] = (29.7997m, 66.8455m),
        ["Musakhel"] = (30.8594m, 69.8221m), ["Nasirabad"] = (28.5833m, 67.8833m),
        ["Nushki"] = (29.5522m, 66.0229m), ["Panjgur"] = (26.9719m, 64.0946m),
        ["Pishin"] = (30.5818m, 66.9941m), ["Quetta"] = (30.1841m, 67.0014m),
        ["Shaheed Sikandarabad"] = (28.8000m, 67.3000m), ["Sherani"] = (28.2664m, 67.3763m),
        ["Sibi"] = (29.5430m, 67.8773m), ["Sohbatpur"] = (28.5204m, 68.5430m),
        ["Washuk"] = (27.7273m, 64.8097m), ["Zhob"] = (31.3408m, 69.4493m),
        ["Ziarat"] = (30.3824m, 67.7256m),

        // ── Gilgit Baltistan (14 districts) ───────────────────────
        ["Astore"] = (35.0500m, 75.0000m), ["Darel"] = (35.1889m, 73.4318m),
        ["Diamir"] = (35.2000m, 73.3000m), ["Ghanche"] = (35.0589m, 76.2967m),
        ["Ghizer"] = (36.0000m, 73.5000m), ["Gilgit"] = (35.9187m, 74.3125m),
        ["Gupis-Yasin"] = (36.1000m, 73.2000m), ["Hunza"] = (36.3269m, 74.6614m),
        ["Kharmang"] = (34.9449m, 76.2175m), ["Nagar"] = (36.2754m, 74.7196m),
        ["Rondu"] = (35.5000m, 75.0000m), ["Shigar"] = (35.4238m, 75.7391m),
        ["Skardu"] = (35.2979m, 75.6337m), ["Tangir"] = (35.5000m, 73.5000m),

        // ── Islamabad Capital Territory ───────────────────────────
        ["Islamabad"] = (33.7215m, 73.0433m),

        // ── Khyber Pakhtunkhwa (35 districts) ─────────────────────
        ["Abbottabad"] = (34.1463m, 73.2117m), ["Bajaur"] = (34.7500m, 71.5000m),
        ["Bannu"] = (32.9853m, 70.6040m), ["Batagram"] = (34.6833m, 73.5333m),
        ["Buner"] = (34.4333m, 72.4833m), ["Charsadda"] = (34.1482m, 71.7406m),
        ["Chitral Lower"] = (35.8518m, 71.7866m), ["Chitral Upper"] = (36.1000m, 71.6000m),
        ["D. I. Khan"] = (31.8317m, 70.9017m), ["Hangu"] = (33.5320m, 71.0595m),
        ["Haripur"] = (33.9978m, 72.9349m), ["Karak"] = (33.1163m, 71.0935m),
        ["Khyber"] = (34.1000m, 71.2500m), ["Kohat"] = (33.5869m, 71.4414m),
        ["Kohistan Lower"] = (35.3000m, 73.1000m), ["Kohistan Upper"] = (35.7000m, 73.4000m),
        ["Kolai Palas Kohistan"] = (35.4000m, 73.2000m), ["Kurram"] = (33.9000m, 70.3000m),
        ["Lakki Marwat"] = (32.6000m, 70.9167m), ["Lower Dir"] = (35.2000m, 71.8833m),
        ["Malakand"] = (34.5656m, 71.9304m), ["Mansehra"] = (34.3302m, 73.1968m),
        ["Mardan"] = (34.1979m, 72.0496m), ["Mohmand"] = (34.5500m, 71.3000m),
        ["North Waziristan"] = (33.3000m, 70.1000m), ["Nowshera"] = (34.0158m, 71.9812m),
        ["Orakzai"] = (33.7000m, 70.8000m), ["Peshawar"] = (34.0080m, 71.5785m),
        ["Shangla"] = (34.8873m, 72.5991m), ["South Waziristan"] = (32.3500m, 69.7500m),
        ["Swabi"] = (34.1202m, 72.4698m), ["Swat"] = (35.3792m, 72.1756m),
        ["Tank"] = (32.2171m, 70.3832m), ["Tor Ghar"] = (34.7500m, 72.5500m),
        ["Upper Dir"] = (35.2074m, 71.8768m),

        // ── Sindh (29 districts incl. 6 Karachi) ──────────────────
        ["Badin"] = (24.6560m, 68.8370m), ["Central Karachi"] = (24.9416m, 67.0234m),
        ["Dadu"] = (26.7303m, 67.7769m), ["East Karachi"] = (24.9000m, 67.1000m),
        ["Ghotki"] = (28.0044m, 69.3157m), ["Hyderabad"] = (25.3960m, 68.3578m),
        ["Jacobabad"] = (28.2819m, 68.4376m), ["Jamshoro"] = (25.4361m, 68.2802m),
        ["Kashmore"] = (28.4326m, 69.5836m), ["Khairpur"] = (27.5295m, 68.7592m),
        ["Korangi Karachi"] = (24.8500m, 67.1000m), ["Larkana"] = (27.5590m, 68.2120m),
        ["Malir Karachi"] = (24.9500m, 67.2000m), ["Matiari"] = (25.5971m, 68.4467m),
        ["Mirpur Khas"] = (25.5276m, 69.0126m), ["Naushahro Feroze"] = (26.8437m, 68.1289m),
        ["Qambar Shahdadkot"] = (27.5833m, 68.0000m), ["Sanghar"] = (26.0469m, 68.9492m),
        ["Shaheed Benazir Abad"] = (26.2486m, 68.4099m), ["Shikarpur"] = (27.9556m, 68.6382m),
        ["South Karachi"] = (24.8000m, 67.0000m), ["Sujawal"] = (24.6036m, 68.0776m),
        ["Sukkur"] = (27.7032m, 68.8589m), ["Tando Allahyar"] = (25.4605m, 68.7174m),
        ["Tando Muhammad Khan"] = (25.1238m, 68.5368m), ["Tharparkar"] = (24.7500m, 70.0000m),
        ["Thatta"] = (24.7474m, 67.9235m), ["Umerkot"] = (25.3630m, 69.7418m),
        ["West Karachi"] = (24.9500m, 66.9500m),
    };
}
