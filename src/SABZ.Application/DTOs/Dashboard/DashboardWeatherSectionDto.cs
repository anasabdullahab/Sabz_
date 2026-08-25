using SABZ.Application.DTOs.Weather;

namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// External current weather for the farm location (Prompt 12 dashboard
/// section), reused cleanly from the existing Prompt 3 weather service.
/// This is EXTERNAL provider data (Open-Meteo) and is always clearly
/// distinguished from farmer-recorded SABZ data. Null on the dashboard when
/// the farm has no GPS coordinates or the provider is unavailable - the
/// dashboard itself never fails because of weather.
/// </summary>
public class DashboardWeatherSectionDto
{
    /// <summary>Weather data provider name (e.g. "Open-Meteo").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this external data was retrieved (UTC).</summary>
    public DateTime RetrievedAt { get; set; }

    /// <summary>Current external weather observation.</summary>
    public CurrentWeatherDto? Current { get; set; }

    /// <summary>Always present: weather is external data, not farmer-recorded SABZ data.</summary>
    public string Note { get; set; } = string.Empty;
}
