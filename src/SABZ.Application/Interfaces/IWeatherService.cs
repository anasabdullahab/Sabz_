using SABZ.Application.DTOs.Weather;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Application-level weather service.
/// Validates farm ownership and coordinates, applies caching,
/// delegates to IWeatherProvider, and returns clean SABZ responses.
/// </summary>
public interface IWeatherService
{
    /// <summary>Get current weather for the specified farm.</summary>
    Task<WeatherResponseDto> GetCurrentWeatherAsync(Guid userId, Guid farmId, CancellationToken ct = default);

    /// <summary>Get multi-day forecast for the specified farm.</summary>
    Task<WeatherResponseDto> GetForecastAsync(Guid userId, Guid farmId, CancellationToken ct = default);
}
