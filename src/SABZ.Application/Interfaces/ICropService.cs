using SABZ.Application.DTOs.Crops;

namespace SABZ.Application.Interfaces;

public interface ICropService
{
    Task<CropResponseDto> CreateCropAsync(Guid userId, Guid farmId, CreateCropDto dto);
    Task<List<CropResponseDto>> GetCropsByFarmAsync(Guid userId, Guid farmId);
    Task<CropResponseDto> GetCropByIdAsync(Guid userId, Guid cropId);
    Task<CropResponseDto> UpdateCropAsync(Guid userId, Guid cropId, UpdateCropDto dto);
    Task DeleteCropAsync(Guid userId, Guid cropId);
}
