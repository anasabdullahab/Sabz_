using SABZ.Application.DTOs.Farms;
using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface IFarmRepository
{
    Task<Farm?> GetByIdAsync(Guid id);
    Task<List<Farm>> GetByUserIdAsync(Guid userId);
    Task AddAsync(Farm farm);
    void Update(Farm farm);
    void Remove(Farm farm);
    Task SaveChangesAsync();
}
