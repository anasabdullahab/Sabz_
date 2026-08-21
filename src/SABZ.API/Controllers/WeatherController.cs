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

    public WeatherController(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    /// <summary>
    /// Get the current weather for a farm using its GPS coordinates.
    /// Requires authentication; the authenticated user must own the farm.
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentWeather(Guid farmId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _weatherService.GetCurrentWeatherAsync(userId, farmId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get the multi-day weather forecast for a farm using its GPS coordinates.
    /// Requires authentication; the authenticated user must own the farm.
    /// </summary>
    [HttpGet("forecast")]
    public async Task<IActionResult> GetForecast(Guid farmId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _weatherService.GetForecastAsync(userId, farmId, ct);
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
