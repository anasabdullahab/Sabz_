using SABZ.Application.DTOs.Weather;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Smart weather action alerts engine (hackathon feature).
/// Runs rule-based checks over the Open-Meteo forecast for a farm and
/// returns plain-English action alerts (rain, fungal, wind, frost, heat).
/// </summary>
public interface IWeatherAlertService
{
    Task<WeatherAlertsResponseDto> GetAlertsAsync(Guid userId, Guid farmId, CancellationToken ct = default);
}
