using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface ICropRepository
{
    Task<Crop?> GetByIdAsync(Guid id);
    Task<List<Crop>> GetByFarmIdAsync(Guid farmId);

    /// <summary>
    /// Historical crop records usable for previous-crop determination:
    /// all non-"Planned" records of the farm, including the catalog link,
    /// ordered most-recent first (planting date, falling back to creation date).
    /// </summary>
    Task<List<Crop>> GetHistoryByFarmIdAsync(Guid farmId);

    Task AddAsync(Crop crop);
    void Update(Crop crop);
    void Remove(Crop crop);
    Task SaveChangesAsync();
}
