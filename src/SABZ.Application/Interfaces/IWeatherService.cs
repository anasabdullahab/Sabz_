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
    Task<WeatherResponseDto> GetCurrentWeatherAsync(Guid userId, Guid farmId,
        double? overrideLat = null, double? overrideLon = null, CancellationToken ct = default);

    /// <summary>Get multi-day forecast for the specified farm.</summary>
    Task<WeatherResponseDto> GetForecastAsync(Guid userId, Guid farmId,
        double? overrideLat = null, double? overrideLon = null, CancellationToken ct = default);

    /// <summary>
    /// Get a tehsil-based weather preview without requiring a farm.
    /// Used by the dashboard onboarding layout for accounts with zero farms.
    /// </summary>
    Task<WeatherPreviewDto> GetPreviewAsync(int tehsilId, CancellationToken ct = default);

    /// <summary>Reverse-geocode coordinates to a human-readable place name.</summary>
    Task<ReverseGeocodeDto> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);
}
