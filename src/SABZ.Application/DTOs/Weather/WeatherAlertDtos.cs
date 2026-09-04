namespace SABZ.Application.DTOs.Weather;

/// <summary>
/// Smart weather action alerts response (hackathon feature).
/// Rule-based alerts derived from the Open-Meteo forecast, with
/// optional crop-growth-stage context for fungal risk evaluation.
/// </summary>
public class WeatherAlertsResponseDto
{
    public Guid FarmId { get; set; }
    public Guid UserId { get; set; }
    public List<WeatherAlertDto> Alerts { get; set; } = new();
    public DateTime EvaluatedAt { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
}

/// <summary>One actionable farm alert with a plain-English recommendation.</summary>
public class WeatherAlertDto
{
    /// <summary>Machine-readable kind: RainRisk, FungalRisk, WindAlert, FrostRisk, HeatStress.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Severity: Info, Warning, Danger.</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Short farmer-facing title (e.g. "Rain Expected Tomorrow").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Plain-English action the farmer should take.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Which day(s) the alert applies to (e.g. "Tomorrow", "Today").</summary>
    public string When { get; set; } = string.Empty;

    /// <summary>The measured value that triggered the rule (e.g. "70% rain chance", "32 km/h wind").</summary>
    public string Trigger { get; set; } = string.Empty;
}
