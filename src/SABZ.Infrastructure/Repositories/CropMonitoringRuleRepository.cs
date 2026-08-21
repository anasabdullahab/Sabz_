using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class CropMonitoringRuleRepository : ICropMonitoringRuleRepository
{
    private readonly SabzDbContext _context;

    public CropMonitoringRuleRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<List<CropMonitoringRule>> GetActiveScheduledForCropAsync(int? cropCatalogId, CancellationToken ct = default)
    {
        // Catalog-specific rules plus general rules (CropCatalogId null),
        // ordered chronologically by day offset.
        return await _context.CropMonitoringRules
            .AsNoTracking()
            .Where(r => r.IsActive
                && r.TriggerType == "Scheduled"
                && (r.CropCatalogId == cropCatalogId || r.CropCatalogId == null))
            .OrderBy(r => r.DayOffsetAfterPlanting)
            .ToListAsync(ct);
    }
}
