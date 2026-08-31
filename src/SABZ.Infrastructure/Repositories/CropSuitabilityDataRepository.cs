using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

/// <summary>
/// Read access to crop suitability reference data.
/// Requirements and regional rules are small reference datasets loaded in single queries.
/// </summary>
public class CropSuitabilityDataRepository : ICropSuitabilityDataRepository
{
    private readonly SabzDbContext _db;

    public CropSuitabilityDataRepository(SabzDbContext db)
    {
        _db = db;
    }

    public async Task<List<CropRequirement>> GetRequirementsAsync(CancellationToken ct = default)
        => await _db.CropRequirements
            .AsNoTracking()
            .Include(r => r.CropCatalog)
            .OrderBy(r => r.CropCatalogId)
            .ToListAsync(ct);

    public async Task<List<RegionalCropSuitability>> GetRegionalRulesAsync(CancellationToken ct = default)
        => await _db.RegionalCropSuitabilities
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<List<CropCatalog>> GetCatalogAsync(CancellationToken ct = default)
        => await _db.CropCatalog
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
}
