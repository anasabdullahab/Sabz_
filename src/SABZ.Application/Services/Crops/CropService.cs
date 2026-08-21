using SABZ.Application.DTOs.Crops;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Crops;

public class CropService : ICropService
{
    private readonly ICropRepository _cropRepository;
    private readonly IFarmRepository _farmRepository;

    private static readonly HashSet<string> ValidSeasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rabi", "Kharif", "Other"
    };

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "Harvested", "Failed", "Planned"
    };

    private static readonly HashSet<string> ValidGrowthStages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sowing", "Germination", "Vegetative", "Flowering", "Fruiting", "Maturity", "Harvesting"
    };

    public CropService(ICropRepository cropRepository, IFarmRepository farmRepository)
    {
        _cropRepository = cropRepository;
        _farmRepository = farmRepository;
    }

    public async Task<CropResponseDto> CreateCropAsync(Guid userId, Guid farmId, CreateCropDto dto)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
                   ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        ValidateSeason(dto.Season);
        if (!string.IsNullOrWhiteSpace(dto.GrowthStage))
            ValidateGrowthStage(dto.GrowthStage);

        var status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status;
        ValidateStatus(status);

        var crop = new Crop
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            CropCatalogId = dto.CropCatalogId,
            CropName = dto.CropName,
            Season = dto.Season,
            PlantingDate = dto.PlantingDate,
            GrowthStage = dto.GrowthStage,
            PreviousCrop = dto.PreviousCrop,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        await _cropRepository.AddAsync(crop);
        await _cropRepository.SaveChangesAsync();

        return MapToResponse(crop);
    }

    public async Task<List<CropResponseDto>> GetCropsByFarmAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
                   ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        var crops = await _cropRepository.GetByFarmIdAsync(farmId);
        return crops.Select(MapToResponse).ToList();
    }

    public async Task<CropResponseDto> GetCropByIdAsync(Guid userId, Guid cropId)
    {
        var crop = await _cropRepository.GetByIdAsync(cropId)
                   ?? throw new NotFoundException("Crop not found.");

        if (crop.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this crop.");

        return MapToResponse(crop);
    }

    public async Task<CropResponseDto> UpdateCropAsync(Guid userId, Guid cropId, UpdateCropDto dto)
    {
        var crop = await _cropRepository.GetByIdAsync(cropId)
                   ?? throw new NotFoundException("Crop not found.");

        if (crop.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this crop.");

        ValidateSeason(dto.Season);
        if (!string.IsNullOrWhiteSpace(dto.GrowthStage))
            ValidateGrowthStage(dto.GrowthStage);

        var status = string.IsNullOrWhiteSpace(dto.Status) ? crop.Status : dto.Status;
        ValidateStatus(status);

        crop.CropName = dto.CropName;
        crop.CropCatalogId = dto.CropCatalogId;
        crop.Season = dto.Season;
        crop.PlantingDate = dto.PlantingDate;
        crop.GrowthStage = dto.GrowthStage;
        crop.PreviousCrop = dto.PreviousCrop;
        crop.Status = status;
        crop.UpdatedAt = DateTime.UtcNow;

        _cropRepository.Update(crop);
        await _cropRepository.SaveChangesAsync();

        return MapToResponse(crop);
    }

    public async Task DeleteCropAsync(Guid userId, Guid cropId)
    {
        var crop = await _cropRepository.GetByIdAsync(cropId)
                   ?? throw new NotFoundException("Crop not found.");

        if (crop.Farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this crop.");

        _cropRepository.Remove(crop);
        await _cropRepository.SaveChangesAsync();
    }

    private static void ValidateSeason(string season)
    {
        if (!ValidSeasons.Contains(season))
            throw new Domain.Exceptions.ValidationException($"Season must be one of: {string.Join(", ", ValidSeasons)}.");
    }

    private static void ValidateStatus(string status)
    {
        if (!ValidStatuses.Contains(status))
            throw new Domain.Exceptions.ValidationException($"Status must be one of: {string.Join(", ", ValidStatuses)}.");
    }

    private static void ValidateGrowthStage(string growthStage)
    {
        if (!ValidGrowthStages.Contains(growthStage))
            throw new Domain.Exceptions.ValidationException($"Growth stage must be one of: {string.Join(", ", ValidGrowthStages)}.");
    }

    private static CropResponseDto MapToResponse(Crop crop)
    {
        return new CropResponseDto
        {
            Id = crop.Id,
            FarmId = crop.FarmId,
            CropCatalogId = crop.CropCatalogId,
            CropName = crop.CropName,
            Season = crop.Season,
            PlantingDate = crop.PlantingDate,
            GrowthStage = crop.GrowthStage,
            PreviousCrop = crop.PreviousCrop,
            Status = crop.Status,
            CreatedAt = crop.CreatedAt,
            UpdatedAt = crop.UpdatedAt
        };
    }
}
