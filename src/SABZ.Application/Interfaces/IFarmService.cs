using SABZ.Application.DTOs.Crops;
using SABZ.Application.DTOs.Farms;

namespace SABZ.Application.Interfaces;

public interface IFarmService
{
    Task<FarmResponseDto> CreateFarmAsync(Guid userId, CreateFarmDto dto);
    Task<List<FarmResponseDto>> GetFarmsAsync(Guid userId);
    Task<FarmResponseDto> GetFarmByIdAsync(Guid userId, Guid farmId);
    Task<FarmResponseDto> UpdateFarmAsync(Guid userId, Guid farmId, UpdateFarmDto dto);
    Task DeleteFarmAsync(Guid userId, Guid farmId);
}
