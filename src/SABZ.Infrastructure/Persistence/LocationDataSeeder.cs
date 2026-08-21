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

        // One-time cleanup: remove duplicate tehsils that were inserted before
        // name-based deduplication was added (old seed IDs vs new dataset IDs).
        var removed = await CleanupDuplicateTehsilsAsync(db);

        if (inserted > 0 || removed > 0)
        {
            logger.LogInformation("Location seed: inserted {Inserted} new records, removed {Removed} duplicate tehsils.", inserted, removed);
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
}
