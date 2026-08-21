using SABZ.Application.DTOs.Weather;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Abstraction over external weather data providers.
/// Implementations fetch raw data from a specific API (e.g. Open-Meteo)
/// and map it to SABZ weather contracts.
/// </summary>
public interface IWeatherProvider
{
    /// <summary>Provider display name (e.g. "Open-Meteo").</summary>
    string SourceName { get; }

    /// <summary>Retrieve current weather for the given coordinates.</summary>
    Task<CurrentWeatherDto> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken ct);

    /// <summary>Retrieve a multi-day forecast for the given coordinates.</summary>
    Task<ForecastDto> GetForecastAsync(double latitude, double longitude, int days, CancellationToken ct);
}
