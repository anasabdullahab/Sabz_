using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class CropCatalogRepository : ICropCatalogRepository
{
    private readonly SabzDbContext _context;

    public CropCatalogRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<List<CropCatalog>> GetCatalogAsync(CancellationToken ct = default)
        => await _context.CropCatalog
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
}
