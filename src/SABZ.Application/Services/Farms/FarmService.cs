using SABZ.Application.DTOs.Farms;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Farms;

public class FarmService : IFarmService
{
    private readonly IFarmRepository _farmRepository;
    private readonly ILocationRepository _locationRepository;

    public FarmService(IFarmRepository farmRepository, ILocationRepository locationRepository)
    {
        _farmRepository = farmRepository;
        _locationRepository = locationRepository;
    }

    public async Task<FarmResponseDto> CreateFarmAsync(Guid userId, CreateFarmDto dto)
    {
        await ValidateLocationConsistencyAsync(dto.ProvinceId!.Value, dto.DistrictId!.Value, dto.TehsilId!.Value);

        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FarmName = dto.FarmName,
            ProvinceId = dto.ProvinceId.Value,
            DistrictId = dto.DistrictId.Value,
            TehsilId = dto.TehsilId.Value,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            FarmSize = dto.FarmSize,
            FarmSizeUnit = dto.FarmSizeUnit,
            SoilType = dto.SoilType,
            IrrigationType = dto.IrrigationType,
            CreatedAt = DateTime.UtcNow
        };

        await _farmRepository.AddAsync(farm);
        await _farmRepository.SaveChangesAsync();

        return await BuildFarmResponseAsync(farm);
    }

    public async Task<List<FarmResponseDto>> GetFarmsAsync(Guid userId)
    {
        var farms = await _farmRepository.GetByUserIdAsync(userId);
        var responses = new List<FarmResponseDto>();
        foreach (var farm in farms)
            responses.Add(await BuildFarmResponseAsync(farm));
        return responses;
    }

    public async Task<FarmResponseDto> GetFarmByIdAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
                   ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return await BuildFarmResponseAsync(farm);
    }

    public async Task<FarmResponseDto> UpdateFarmAsync(Guid userId, Guid farmId, UpdateFarmDto dto)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
                   ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        await ValidateLocationConsistencyAsync(dto.ProvinceId!.Value, dto.DistrictId!.Value, dto.TehsilId!.Value);

        farm.FarmName = dto.FarmName;
        farm.ProvinceId = dto.ProvinceId.Value;
        farm.DistrictId = dto.DistrictId.Value;
        farm.TehsilId = dto.TehsilId.Value;
        farm.Latitude = dto.Latitude;
        farm.Longitude = dto.Longitude;
        farm.FarmSize = dto.FarmSize;
        farm.FarmSizeUnit = dto.FarmSizeUnit;
        farm.SoilType = dto.SoilType;
        farm.IrrigationType = dto.IrrigationType;
        farm.UpdatedAt = DateTime.UtcNow;

        _farmRepository.Update(farm);
        await _farmRepository.SaveChangesAsync();

        return await BuildFarmResponseAsync(farm);
    }

    public async Task DeleteFarmAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
                   ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        _farmRepository.Remove(farm);
        await _farmRepository.SaveChangesAsync();
    }

    private async Task ValidateLocationConsistencyAsync(int provinceId, int districtId, int tehsilId)
    {
        if (!await _locationRepository.ProvinceExistsAsync(provinceId))
            throw new Domain.Exceptions.ValidationException("Selected province does not exist.");

        if (!await _locationRepository.DistrictExistsAndBelongsToProvinceAsync(districtId, provinceId))
            throw new Domain.Exceptions.ValidationException("Selected district does not belong to the selected province.");

        if (!await _locationRepository.TehsilExistsAndBelongsToDistrictAsync(tehsilId, districtId))
            throw new Domain.Exceptions.ValidationException("Selected tehsil does not belong to the selected district.");
    }

    private async Task<FarmResponseDto> BuildFarmResponseAsync(Farm farm)
    {
        // Ensure navigation properties are populated for location names
        if (farm.Province is null || farm.District is null || farm.Tehsil is null)
        {
            farm = await _farmRepository.GetByIdAsync(farm.Id) ?? farm;
        }

        return new FarmResponseDto
        {
            Id = farm.Id,
            FarmName = farm.FarmName,
            ProvinceId = farm.ProvinceId,
            ProvinceName = farm.Province?.Name ?? string.Empty,
            DistrictId = farm.DistrictId,
            DistrictName = farm.District?.Name ?? string.Empty,
            TehsilId = farm.TehsilId,
            TehsilName = farm.Tehsil?.Name ?? string.Empty,
            Latitude = farm.Latitude,
            Longitude = farm.Longitude,
            FarmSize = farm.FarmSize,
            FarmSizeUnit = farm.FarmSizeUnit,
            SoilType = farm.SoilType,
            IrrigationType = farm.IrrigationType,
            CreatedAt = farm.CreatedAt,
            UpdatedAt = farm.UpdatedAt
        };
    }
}
