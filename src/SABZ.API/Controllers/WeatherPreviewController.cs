using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

/// <summary>
/// Location-based weather endpoints that are not tied to a farm.
/// Used by the dashboard onboarding layout before the farmer has
/// created their first farm (regional weather preview).
/// </summary>
[ApiController]
[Route("api/weather")]
[Authorize]
public class WeatherPreviewController : ControllerBase
{
    private readonly IWeatherService _weatherService;

    public WeatherPreviewController(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    /// <summary>
    /// Get a weather preview for a tehsil (current conditions + multi-day forecast).
    /// No farm required — resolves coordinates from the tehsil seed data.
    /// </summary>
    [HttpGet("preview")]
    public async Task<IActionResult> GetPreview([FromQuery] int tehsilId, CancellationToken ct = default)
    {
        var result = await _weatherService.GetPreviewAsync(tehsilId, ct);
        return Ok(result);
    }
}
