namespace SABZ.Application.Interfaces;

/// <summary>
/// Configuration for the weather subsystem.
/// Bound from appsettings.json section "Weather".
/// </summary>
public class WeatherSettings
{
    public const string SectionName = "Weather";

    /// <summary>Open-Meteo base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.open-meteo.com";

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Cache duration for current weather (minutes).</summary>
    public int CurrentCacheMinutes { get; set; } = 15;

    /// <summary>Cache duration for forecast data (minutes).</summary>
    public int ForecastCacheMinutes { get; set; } = 60;

    /// <summary>Default forecast horizon (days).</summary>
    public int ForecastDays { get; set; } = 7;

    /// <summary>
    /// Decimal places to round coordinates for cache keys.
    /// 2 decimals ≈ ~1.1 km resolution — avoids excessive cache entries
    /// while still distinguishing nearby farms.
    /// </summary>
    public int CoordinatePrecision { get; set; } = 2;
}
