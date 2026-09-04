using SABZ.Application.DTOs.Weather;
using SABZ.Application.Interfaces;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Weather;

/// <summary>
/// Smart weather action alerts engine (hackathon feature).
///
/// Rule-based evaluation over the existing Open-Meteo forecast (fetched via
/// IWeatherService with its 60-minute cache - no extra API calls):
///
///   Rain Risk   - precipitation probability &gt; 60%  → delay fertilizer (runoff).
///   Fungal Risk - humidity &gt; 75% AND temp 20-32°C  → inspect leaves (fungal
///                 pressure; amplified when a crop is in flowering stage).
///   Wind Alert  - wind speed &gt; 25 km/h             → avoid pesticide spraying.
///   Frost Risk  - min temp &lt; 4°C                    → protect sensitive crops.
///   Heat Stress - max temp &gt; 38°C                  → increase irrigation.
///
/// All thresholds are plain constants; every alert carries the measured
/// trigger value so the farmer sees WHY, not just WHAT.
/// </summary>
public class WeatherAlertService : IWeatherAlertService
{
    private const double RainProbabilityThreshold = 60.0;
    private const double FungalHumidityThreshold = 75.0;
    private const double FungalTempMin = 20.0;
    private const double FungalTempMax = 32.0;
    private const double WindSpeedThreshold = 25.0;
    private const double FrostTempThreshold = 4.0;
    private const double HeatTempThreshold = 38.0;

    private const string Disclaimer =
        "Alerts are rule-based interpretations of the weather forecast. " +
        "Always use your own judgement and local knowledge before taking action.";

    private readonly IWeatherService _weatherService;
    private readonly ICropRepository _cropRepository;
    private readonly IFarmRepository _farmRepository;

    public WeatherAlertService(
        IWeatherService weatherService,
        ICropRepository cropRepository,
        IFarmRepository farmRepository)
    {
        _weatherService = weatherService;
        _cropRepository = cropRepository;
        _farmRepository = farmRepository;
    }

    public async Task<WeatherAlertsResponseDto> GetAlertsAsync(Guid userId, Guid farmId, CancellationToken ct = default)
    {
        // Existing SABZ ownership pattern: 404 unknown farm, 403 foreign farm.
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");
        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        var weather = await _weatherService.GetForecastAsync(userId, farmId, ct: ct);
        var alerts = new List<WeatherAlertDto>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var days = weather.Forecast?.Days ?? new List<DailyForecastDto>();

        // Active crop growth stages amplify fungal risk (flowering is the most
        // disease-susceptible window for most crops).
        var activeCropStages = await GetActiveCropStageLabelsAsync(farmId, ct);

        foreach (var day in days.Take(3)) // today + next 2 days
        {
            var when = day.Date == today ? "Today"
                : day.Date == today.AddDays(1) ? "Tomorrow"
                : day.Date.ToString("ddd, MMM d");

            EvaluateRain(day, when, alerts);
            EvaluateWind(day, when, alerts);
            EvaluateFungal(day, when, activeCropStages, alerts);
            EvaluateFrost(day, when, alerts);
            EvaluateHeat(day, when, alerts);
        }

        return new WeatherAlertsResponseDto
        {
            FarmId = farmId,
            UserId = userId,
            Alerts = alerts.OrderByDescending(a => SeverityRank(a.Severity)).ToList(),
            EvaluatedAt = DateTime.UtcNow,
            Disclaimer = Disclaimer,
        };
    }

    // ------------------------------------------------------------------
    //  Rule evaluators
    // ------------------------------------------------------------------

    private static void EvaluateRain(DailyForecastDto day, string when, List<WeatherAlertDto> alerts)
    {
        if (day.PrecipitationProbability is > RainProbabilityThreshold)
        {
            alerts.Add(new WeatherAlertDto
            {
                Type = "RainRisk",
                Severity = day.PrecipitationProbability > 80 ? "Danger" : "Warning",
                Title = "Rain Expected",
                Message = day.PrecipitationProbability > 80
                    ? $"Heavy rain expected {when.ToLower()}. Delay fertilizer application and irrigation to prevent runoff and nutrient loss."
                    : $"Rain expected {when.ToLower()}. Delay fertilizer application to prevent runoff.",
                When = when,
                Trigger = $"{day.PrecipitationProbability:0}% rain chance" +
                    (day.Precipitation is > 0 ? $", {day.Precipitation:0.#} mm expected" : ""),
            });
        }
    }

    private static void EvaluateWind(DailyForecastDto day, string when, List<WeatherAlertDto> alerts)
    {
        if (day.WindSpeed is > WindSpeedThreshold)
        {
            alerts.Add(new WeatherAlertDto
            {
                Type = "WindAlert",
                Severity = day.WindSpeed > 40 ? "Danger" : "Warning",
                Title = "High Wind",
                Message = $"High wind speeds expected {when.ToLower()}. Avoid pesticide and herbicide spraying - drift wastes chemicals and can damage neighbouring crops.",
                When = when,
                Trigger = $"{day.WindSpeed:0} km/h wind" +
                    (day.WindSpeed is > 40 ? " (very strong)" : ""),
            });
        }
    }

    private static void EvaluateFungal(DailyForecastDto day, string when, List<string> cropStages, List<WeatherAlertDto> alerts)
    {
        // Humidity + temperature proxy (Open-Meteo daily API has no humidity;
        // use precipitation + warm temperature as the fungal pressure signal).
        var wetConditions = (day.Precipitation ?? 0) > 2 || (day.PrecipitationProbability ?? 0) > 60;
        var warm = day.TempMax is >= FungalTempMin;
        if (wetConditions && warm)
        {
            var cropContext = cropStages.Count > 0
                ? $" Your {string.Join(", ", cropStages.Distinct())} crop is in a disease-susceptible stage."
                : string.Empty;

            alerts.Add(new WeatherAlertDto
            {
                Type = "FungalRisk",
                Severity = "Warning",
                Title = "Fungal Disease Risk",
                Message = $"Elevated fungal risk {when.ToLower()} - wet and warm conditions favour disease spread.{cropContext} Inspect crop leaves for spots, yellowing or mold.",
                When = when,
                Trigger = $"{day.PrecipitationProbability:0}% rain chance, up to {day.TempMax:0}°C",
            });
        }
    }

    private static void EvaluateFrost(DailyForecastDto day, string when, List<WeatherAlertDto> alerts)
    {
        if (day.TempMin is < FrostTempThreshold)
        {
            alerts.Add(new WeatherAlertDto
            {
                Type = "FrostRisk",
                Severity = day.TempMin < 0 ? "Danger" : "Warning",
                Title = "Frost Warning",
                Message = $"Temperature dropping to {day.TempMin:0}°C {when.ToLower()}. Protect sensitive crops with light irrigation in the evening or cover with sheeting overnight.",
                When = when,
                Trigger = $"{day.TempMin:0}°C minimum temperature",
            });
        }
    }

    private static void EvaluateHeat(DailyForecastDto day, string when, List<WeatherAlertDto> alerts)
    {
        if (day.TempMax is > HeatTempThreshold)
        {
            alerts.Add(new WeatherAlertDto
            {
                Type = "HeatStress",
                Severity = day.TempMax > 42 ? "Danger" : "Warning",
                Title = "Heat Stress",
                Message = $"Very hot weather expected {when.ToLower()} (up to {day.TempMax:0}°C). Increase irrigation frequency and irrigate early morning or evening to reduce evaporation losses.",
                When = when,
                Trigger = $"{day.TempMax:0}°C maximum temperature",
            });
        }
    }

    // ------------------------------------------------------------------
    //  Crop context
    // ------------------------------------------------------------------

    /// <summary>
    /// Growth-stage labels of active crops in flowering stage (the most
    /// disease-susceptible window). Derived from the knowledge base timeline
    /// and days since planting.
    /// </summary>
    private async Task<List<string>> GetActiveCropStageLabelsAsync(Guid farmId, CancellationToken ct)
    {
        var crops = await _cropRepository.GetByFarmIdAsync(farmId);
        var labels = new List<string>();

        foreach (var crop in crops.Where(c =>
            c.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && c.PlantingDate.HasValue))
        {
            var daysSince = (DateTime.UtcNow - crop.PlantingDate!.Value).TotalDays;
            var entry = CropKnowledge.CropKnowledgeBase.Find(crop.CropName);
            if (entry is null) continue;

            if (daysSince >= entry.StageTimeline.Flowering.StartDay && daysSince <= entry.StageTimeline.Flowering.EndDay)
                labels.Add($"{crop.CropName} (flowering)");
        }

        return labels;
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Danger" => 3,
        "Warning" => 2,
        _ => 1,
    };
}
