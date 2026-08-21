namespace SABZ.Application.DTOs.Weather;

/// <summary>
/// Current weather observation for a farm location.
/// All values use metric units: Celsius, km/h, mm, degrees.
/// </summary>
public class CurrentWeatherDto
{
    /// <summary>Air temperature at 2 m above ground (°C).</summary>
    public double? Temperature { get; set; }

    /// <summary>Apparent (feels-like) temperature (°C).</summary>
    public double? ApparentTemperature { get; set; }

    /// <summary>Relative humidity at 2 m (%).</summary>
    public double? RelativeHumidity { get; set; }

    /// <summary>Total precipitation (mm).</summary>
    public double? Precipitation { get; set; }

    /// <summary>Rain amount (mm).</summary>
    public double? Rain { get; set; }

    /// <summary>Wind speed at 10 m (km/h).</summary>
    public double? WindSpeed { get; set; }

    /// <summary>Wind direction at 10 m (degrees, 0-360).</summary>
    public double? WindDirection { get; set; }

    /// <summary>Wind gusts at 10 m (km/h).</summary>
    public double? WindGusts { get; set; }

    /// <summary>Cloud cover (%).</summary>
    public double? CloudCover { get; set; }

    /// <summary>WMO weather interpretation code.</summary>
    public int? WeatherCode { get; set; }

    /// <summary>True if daytime, false if nighttime.</summary>
    public bool? IsDay { get; set; }

    /// <summary>Observation timestamp (UTC).</summary>
    public DateTime? ObservationTime { get; set; }
}
