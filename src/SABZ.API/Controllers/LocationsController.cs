using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationsController : ControllerBase
{
    private readonly ILocationRepository _locationRepository;

    public LocationsController(ILocationRepository locationRepository)
    {
        _locationRepository = locationRepository;
    }

    [HttpGet("provinces")]
    public async Task<IActionResult> GetProvinces()
    {
        var provinces = await _locationRepository.GetProvincesAsync();
        return Ok(provinces);
    }

    [HttpGet("provinces/{provinceId:int}/districts")]
    public async Task<IActionResult> GetDistricts(int provinceId)
    {
        var districts = await _locationRepository.GetDistrictsByProvinceAsync(provinceId);
        return Ok(districts);
    }

    [HttpGet("districts/{districtId:int}/tehsils")]
    public async Task<IActionResult> GetTehsils(int districtId)
    {
        var tehsils = await _locationRepository.GetTehsilsByDistrictAsync(districtId);
        return Ok(tehsils);
    }
}
