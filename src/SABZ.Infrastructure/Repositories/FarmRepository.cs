using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class FarmRepository : IFarmRepository
{
    private readonly SabzDbContext _context;

    public FarmRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<Farm?> GetByIdAsync(Guid id)
    {
        return await _context.Farms
            .Include(f => f.Province)
            .Include(f => f.District)
            .Include(f => f.Tehsil)
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<List<Farm>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Farms
            .Include(f => f.Province)
            .Include(f => f.District)
            .Include(f => f.Tehsil)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Farm farm)
    {
        await _context.Farms.AddAsync(farm);
    }

    public void Update(Farm farm)
    {
        _context.Farms.Update(farm);
    }

    public void Remove(Farm farm)
    {
        _context.Farms.Remove(farm);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
