using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class DiseaseInformationRepository : IDiseaseInformationRepository
{
    private readonly SabzDbContext _context;

    public DiseaseInformationRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<List<DiseaseInformation>> GetActiveForCropAsync(int? cropCatalogId, CancellationToken ct = default)
    {
        // Crop-specific entries plus general guidance (CropCatalogId null).
        return await _context.DiseaseInformations
            .AsNoTracking()
            .Where(d => d.IsActive && (d.CropCatalogId == cropCatalogId || d.CropCatalogId == null))
            .OrderBy(d => d.DiseaseName)
            .ToListAsync(ct);
    }
}
