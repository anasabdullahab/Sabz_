using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/weather")]
[Authorize]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;
    private readonly IWeatherAlertService _weatherAlertService;

    public WeatherController(IWeatherService weatherService, IWeatherAlertService weatherAlertService)
    {
        _weatherService = weatherService;
        _weatherAlertService = weatherAlertService;
    }

    /// <summary>
    /// Smart weather action alerts (hackathon feature): rule-based, plain-English
    /// farm alerts derived from the forecast (rain risk, fungal risk, wind,
    /// frost, heat stress) with active-crop growth-stage context.
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts(Guid farmId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _weatherAlertService.GetAlertsAsync(userId, farmId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get the current weather for a farm.
    /// Optional query params latitude/longitude override the farm's stored coordinates
    /// (used by the frontend "Locate Me" button for device GPS precision).
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentWeather(
        Guid farmId,
        [FromQuery] double? latitude = null,
        [FromQuery] double? longitude = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _weatherService.GetCurrentWeatherAsync(userId, farmId, latitude, longitude, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get the multi-day weather forecast for a farm.
    /// Optional query params latitude/longitude override the farm's stored coordinates.
    /// </summary>
    [HttpGet("forecast")]
    public async Task<IActionResult> GetForecast(
        Guid farmId,
        [FromQuery] double? latitude = null,
        [FromQuery] double? longitude = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _weatherService.GetForecastAsync(userId, farmId, latitude, longitude, ct);
        return Ok(result);
    }

    /// <summary>
    /// Reverse-geocode coordinates to a human-readable place name.
    /// Uses the free Open-Meteo geocoding API (no key required).
    /// </summary>
    [HttpGet("reverse-geocode")]
    public async Task<IActionResult> ReverseGeocode(
        Guid farmId,
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken ct = default)
    {
        // Ownership check still required since this is under the farm route
        _ = GetCurrentUserId(); // validates token
        var result = await _weatherService.ReverseGeocodeAsync(latitude, longitude, ct);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new SABZ.Domain.Exceptions.AuthenticationException("Invalid token.");
        return userId;
    }
}
