using Microsoft.EntityFrameworkCore;
using SABZ.Application.DTOs.Locations;
using SABZ.Application.Interfaces;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly SabzDbContext _context;

    public LocationRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<List<LocationDto>> GetProvincesAsync()
    {
        return await _context.Provinces
            .OrderBy(p => p.Name)
            .Select(p => new LocationDto { Id = p.Id, Name = p.Name, NameUrdu = p.NameUrdu })
            .ToListAsync();
    }

    public async Task<List<LocationDto>> GetDistrictsByProvinceAsync(int provinceId)
    {
        return await _context.Districts
            .Where(d => d.ProvinceId == provinceId)
            .OrderBy(d => d.Name)
            .Select(d => new LocationDto { Id = d.Id, Name = d.Name, NameUrdu = d.NameUrdu })
            .ToListAsync();
    }

    public async Task<List<LocationDto>> GetTehsilsByDistrictAsync(int districtId)
    {
        return await _context.Tehsils
            .Where(t => t.DistrictId == districtId)
            .OrderBy(t => t.Name)
            .Select(t => new LocationDto { Id = t.Id, Name = t.Name, NameUrdu = t.NameUrdu })
            .ToListAsync();
    }

    public async Task<bool> ProvinceExistsAsync(int provinceId)
    {
        return await _context.Provinces.AnyAsync(p => p.Id == provinceId);
    }

    public async Task<bool> DistrictExistsAndBelongsToProvinceAsync(int districtId, int provinceId)
    {
        return await _context.Districts.AnyAsync(d => d.Id == districtId && d.ProvinceId == provinceId);
    }

    public async Task<bool> TehsilExistsAndBelongsToDistrictAsync(int tehsilId, int districtId)
    {
        return await _context.Tehsils.AnyAsync(t => t.Id == tehsilId && t.DistrictId == districtId);
    }
}
