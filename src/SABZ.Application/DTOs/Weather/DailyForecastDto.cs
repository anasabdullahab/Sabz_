namespace SABZ.Application.DTOs.Weather;

/// <summary>
/// Single-day forecast values.
/// All values use metric units: Celsius, km/h, mm.
/// </summary>
public class DailyForecastDto
{
    /// <summary>Forecast date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Minimum temperature (°C).</summary>
    public double? TempMin { get; set; }

    /// <summary>Maximum temperature (°C).</summary>
    public double? TempMax { get; set; }

    /// <summary>Total precipitation sum (mm).</summary>
    public double? Precipitation { get; set; }

    /// <summary>Maximum precipitation probability (%).</summary>
    public double? PrecipitationProbability { get; set; }

    /// <summary>Rain sum (mm).</summary>
    public double? Rain { get; set; }

    /// <summary>Maximum wind speed (km/h).</summary>
    public double? WindSpeed { get; set; }

    /// <summary>WMO weather interpretation code.</summary>
    public int? WeatherCode { get; set; }

    /// <summary>Reference evapotranspiration ET0 (mm, FAO Penman-Monteith).</summary>
    public double? Et0 { get; set; }

    /// <summary>Sunrise time (ISO 8601).</summary>
    public string? Sunrise { get; set; }

    /// <summary>Sunset time (ISO 8601).</summary>
    public string? Sunset { get; set; }

    /// <summary>Soil temperature 0-7 cm depth (°C).</summary>
    public double? SoilTemperature { get; set; }

    /// <summary>Soil moisture 0-7 cm depth (m³/m³).</summary>
    public double? SoilMoisture { get; set; }
}
