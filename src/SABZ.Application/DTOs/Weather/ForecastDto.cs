namespace SABZ.Application.DTOs.Weather;

/// <summary>
/// Multi-day forecast container.
/// </summary>
public class ForecastDto
{
    /// <summary>IANA timezone of the forecast location.</summary>
    public string? Timezone { get; set; }

    /// <summary>Daily forecast entries.</summary>
    public List<DailyForecastDto> Days { get; set; } = new();
}
