using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class CropChangeRuleRepository : ICropChangeRuleRepository
{
    private readonly SabzDbContext _context;

    public CropChangeRuleRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<List<CropChangeRule>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        return await _context.CropChangeRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(ct);
    }
}
