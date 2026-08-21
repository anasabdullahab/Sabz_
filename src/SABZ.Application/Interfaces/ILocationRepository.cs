using SABZ.Application.DTOs.Locations;

namespace SABZ.Application.Interfaces;

public interface ILocationRepository
{
    Task<List<LocationDto>> GetProvincesAsync();
    Task<List<LocationDto>> GetDistrictsByProvinceAsync(int provinceId);
    Task<List<LocationDto>> GetTehsilsByDistrictAsync(int districtId);
    Task<bool> ProvinceExistsAsync(int provinceId);
    Task<bool> DistrictExistsAndBelongsToProvinceAsync(int districtId, int provinceId);
    Task<bool> TehsilExistsAndBelongsToDistrictAsync(int tehsilId, int districtId);
}
