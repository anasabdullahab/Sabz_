namespace SABZ.Application.DTOs.Weather;

/// <summary>
/// Top-level weather response for a farm, combining metadata with current/forecast data.
/// </summary>
public class WeatherResponseDto
{
    /// <summary>Farm identifier.</summary>
    public Guid FarmId { get; set; }

    /// <summary>Latitude used for the weather query.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude used for the weather query.</summary>
    public double Longitude { get; set; }

    /// <summary>Weather data provider name (e.g. "Open-Meteo").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this data was retrieved (UTC).</summary>
    public DateTime RetrievedAt { get; set; }

    /// <summary>Current weather observation (null if only forecast was requested).</summary>
    public CurrentWeatherDto? Current { get; set; }

    /// <summary>Multi-day forecast (null if only current was requested).</summary>
    public ForecastDto? Forecast { get; set; }

    /// <summary>Units used in the response.</summary>
    public WeatherUnitsDto Units { get; set; } = new();
}

/// <summary>
/// Describes the measurement units used in the weather response.
/// </summary>
public class WeatherUnitsDto
{
    public string Temperature { get; set; } = "°C";
    public string WindSpeed { get; set; } = "km/h";
    public string Precipitation { get; set; } = "mm";
    public string Humidity { get; set; } = "%";
    public string SoilMoisture { get; set; } = "m³/m³";
}
