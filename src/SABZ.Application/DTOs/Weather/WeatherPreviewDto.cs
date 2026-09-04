namespace SABZ.Application.DTOs.Weather;

/// <summary>
/// Location-based weather preview that does not require a farm.
/// Powers the dashboard onboarding layout shown to accounts with zero farms
/// (regional weather preview card next to the Add Your Farm wizard).
/// </summary>
public class WeatherPreviewDto
{
    /// <summary>Tehsil name the weather was resolved for.</summary>
    public string LocationName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Provider display name (e.g. "Open-Meteo").</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime RetrievedAt { get; set; }

    public CurrentWeatherDto? Current { get; set; }

    public ForecastDto? Forecast { get; set; }
}
