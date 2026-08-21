using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface ICropRepository
{
    Task<Crop?> GetByIdAsync(Guid id);
    Task<List<Crop>> GetByFarmIdAsync(Guid farmId);
    Task AddAsync(Crop crop);
    void Update(Crop crop);
    void Remove(Crop crop);
    Task SaveChangesAsync();
}
