using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class CropRepository : ICropRepository
{
    private readonly SabzDbContext _context;

    public CropRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<Crop?> GetByIdAsync(Guid id)
    {
        return await _context.Crops
            .Include(c => c.Farm)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Crop>> GetByFarmIdAsync(Guid farmId)
    {
        return await _context.Crops
            .Where(c => c.FarmId == farmId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Crop crop)
    {
        await _context.Crops.AddAsync(crop);
    }

    public void Update(Crop crop)
    {
        _context.Crops.Update(crop);
    }

    public void Remove(Crop crop)
    {
        _context.Crops.Remove(crop);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
