using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class CropMonitoringCheckRepository : ICropMonitoringCheckRepository
{
    private readonly SabzDbContext _context;

    public CropMonitoringCheckRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<List<CropMonitoringCheck>> GetByCropIdAsync(Guid cropId, CancellationToken ct = default)
    {
        return await _context.CropMonitoringChecks
            .Include(c => c.Crop).ThenInclude(c => c.Farm)
            .Include(c => c.Crop).ThenInclude(c => c.CropCatalog)
            .Where(c => c.CropId == cropId)
            .OrderBy(c => c.ScheduledDate)
            .ToListAsync(ct);
    }

    public async Task<List<CropMonitoringCheck>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        // Ownership flows through the farm: only checks of the user's farms are returned.
        var farmIds = _context.Farms
            .Where(f => f.UserId == userId)
            .Select(f => f.Id);

        return await _context.CropMonitoringChecks
            .Include(c => c.Crop).ThenInclude(c => c.Farm)
            .Include(c => c.Crop).ThenInclude(c => c.CropCatalog)
            .Where(c => farmIds.Contains(c.FarmId))
            .OrderBy(c => c.ScheduledDate)
            .ToListAsync(ct);
    }

    public async Task<CropMonitoringCheck?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CropMonitoringChecks
            .Include(c => c.Crop).ThenInclude(c => c.Farm)
            .Include(c => c.Crop).ThenInclude(c => c.CropCatalog)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<HashSet<int>> GetExistingRuleIdsAsync(Guid cropId, CancellationToken ct = default)
    {
        var ruleIds = await _context.CropMonitoringChecks
            .Where(c => c.CropId == cropId && c.RuleId != null)
            .Select(c => c.RuleId!.Value)
            .ToListAsync(ct);

        return new HashSet<int>(ruleIds);
    }

    public async Task AddAsync(CropMonitoringCheck check, CancellationToken ct = default)
    {
        await _context.CropMonitoringChecks.AddAsync(check, ct);
    }

    public void Update(CropMonitoringCheck check)
    {
        _context.CropMonitoringChecks.Update(check);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
