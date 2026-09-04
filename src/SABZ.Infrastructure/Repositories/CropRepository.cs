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
            .Include(c => c.CropCatalog)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Crop>> GetByFarmIdAsync(Guid farmId)
    {
        return await _context.Crops
            .Where(c => c.FarmId == farmId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Crop>> GetHistoryByFarmIdAsync(Guid farmId)
    {
        // Planned records are forward-looking and never represent a grown crop.
        return await _context.Crops
            .Include(c => c.CropCatalog)
            .Where(c => c.FarmId == farmId && c.Status != "Planned")
            .OrderByDescending(c => c.PlantingDate ?? c.CreatedAt)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<int?> FindCatalogIdByNameAsync(string cropName)
    {
        if (string.IsNullOrWhiteSpace(cropName)) return null;

        var name = cropName.Trim();
        // "Gram (Chickpea)" -> "Gram" for catalog entries stored with parenthesised names.
        var baseName = name.Contains('(', StringComparison.Ordinal)
            ? name[..name.IndexOf('(', StringComparison.Ordinal)].Trim()
            : name;

        var candidates = await _context.CropCatalog
            .AsNoTracking()
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        if (candidates.Count == 0) return null;

        // Exact name -> exact base name -> prefix (e.g. "Chili" -> "Chili Pepper").
        var match =
            candidates.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(c => string.Equals(c.Name, baseName, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(c => c.Name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase));

        return match?.Id;
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
