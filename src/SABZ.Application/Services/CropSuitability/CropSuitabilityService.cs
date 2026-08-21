using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SABZ.Application.DTOs.CropSuitability;
using SABZ.Application.DTOs.Weather;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.CropSuitability;

/// <summary>
/// Data-driven crop suitability scoring engine.
///
/// Consumes structured crop requirement data and regional rules - no per-crop
/// if/else logic. Missing information is never assumed perfect: unevaluated
/// factors score 0 and are reported in missingData.
///
/// Scores represent a SABZ suitability evaluation based on the currently
/// available data model, not guaranteed agricultural outcomes.
/// </summary>
public class CropSuitabilityService : ICropSuitabilityService
{
    private const string SeasonRabi = "Rabi";
    private const string SeasonKharif = "Kharif";

    private readonly IFarmRepository _farmRepository;
    private readonly ICropSuitabilityDataRepository _suitabilityData;
    private readonly IWeatherService _weatherService;
    private readonly CropSuitabilitySettings _settings;
    private readonly ILogger<CropSuitabilityService> _logger;

    public CropSuitabilityService(
        IFarmRepository farmRepository,
        ICropSuitabilityDataRepository suitabilityData,
        IWeatherService weatherService,
        IOptions<CropSuitabilitySettings> settings,
        ILogger<CropSuitabilityService> logger)
    {
        _farmRepository = farmRepository;
        _suitabilityData = suitabilityData;
        _weatherService = weatherService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CropSuitabilityResponseDto> EvaluateAsync(Guid userId, Guid farmId, string? season, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId);

        var seasonSource = "AutoDetected";
        var evaluationSeason = DetectSeason(DateTime.UtcNow.Month);
        if (!string.IsNullOrWhiteSpace(season))
        {
            evaluationSeason = NormalizeSeason(season);
            seasonSource = "ClientProvided";
        }

        // Load reference data once (no N+1).
        var requirements = await _suitabilityData.GetRequirementsAsync(ct);
        var rules = await _suitabilityData.GetRegionalRulesAsync(ct);

        // Fetch weather at most once per evaluation; reused across all crops.
        var forecast = await TryGetForecastAsync(userId, farmId, farm, ct);

        var results = new List<CropSuitabilityResultDto>();
        foreach (var requirement in requirements
                     .Where(r => string.Equals(r.Season, evaluationSeason, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(r => r.CropCatalogId))
        {
            results.Add(EvaluateCrop(farm, requirement, rules, forecast));
        }

        return new CropSuitabilityResponseDto
        {
            FarmId = farm.Id,
            Location = new FarmLocationDto
            {
                Province = farm.Province?.Name ?? string.Empty,
                District = farm.District?.Name ?? string.Empty,
                Tehsil = farm.Tehsil?.Name ?? string.Empty
            },
            EvaluationSeason = evaluationSeason,
            SeasonSource = seasonSource,
            EvaluatedAt = DateTime.UtcNow,
            WeatherDataAvailable = forecast is not null,
            Crops = results.OrderByDescending(c => c.SuitabilityScore).ThenBy(c => c.CropName).ToList()
        };
    }

    // ------------------------------------------------------------------
    //  Ownership, season, weather
    // ------------------------------------------------------------------

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return farm;
    }

    private static string NormalizeSeason(string season)
    {
        if (string.Equals(season, SeasonRabi, StringComparison.OrdinalIgnoreCase))
            return SeasonRabi;
        if (string.Equals(season, SeasonKharif, StringComparison.OrdinalIgnoreCase))
            return SeasonKharif;

        throw new ValidationException(
            "Invalid season. Supported seasons are 'Rabi' and 'Kharif'.");
    }

    private string DetectSeason(int month)
    {
        var start = _settings.KharifStartMonth;
        var end = _settings.KharifEndMonth;
        var inKharif = start <= end
            ? month >= start && month <= end
            : month >= start || month <= end;
        return inKharif ? SeasonKharif : SeasonRabi;
    }

    /// <summary>
    /// Reuses the existing weather abstraction (with its caching). Missing
    /// coordinates or provider failures degrade gracefully - climate is then
    /// reported as unevaluated, never faked.
    /// </summary>
    private async Task<ForecastDto?> TryGetForecastAsync(Guid userId, Guid farmId, Farm farm, CancellationToken ct)
    {
        if (farm.Latitude is null || farm.Longitude is null)
            return null;

        try
        {
            var response = await _weatherService.GetForecastAsync(userId, farmId, ct);
            return response.Forecast;
        }
        catch (Exception ex) when (ex is not ForbiddenException and not NotFoundException)
        {
            _logger.LogWarning(ex, "Weather unavailable during crop suitability evaluation for farm {FarmId}.", farmId);
            return null;
        }
    }

    // ------------------------------------------------------------------
    //  Scoring
    // ------------------------------------------------------------------

    private CropSuitabilityResultDto EvaluateCrop(
        Farm farm,
        CropRequirement requirement,
        List<RegionalCropSuitability> rules,
        ForecastDto? forecast)
    {
        var result = new CropSuitabilityResultDto
        {
            CropCatalogId = requirement.CropCatalogId,
            CropName = requirement.CropCatalog?.Name ?? $"Crop {requirement.CropCatalogId}"
        };

        // Season: the requirement itself exists for the evaluation season.
        result.FactorScores.Season = _settings.SeasonWeight;
        result.PositiveFactors.Add($"{result.CropName} is a {requirement.Season} season crop matching the evaluation season.");

        EvaluateLocation(farm, requirement, rules, result);
        EvaluateSoil(farm, requirement, result);
        EvaluateWater(farm, requirement, result);
        EvaluateClimate(requirement, forecast, result);

        result.SuitabilityScore =
            result.FactorScores.Location +
            result.FactorScores.Climate +
            result.FactorScores.Soil +
            result.FactorScores.Water +
            result.FactorScores.Season;

        result.SuitabilityLevel = ToLevel(result.SuitabilityScore);
        return result;
    }

    /// <summary>
    /// Geographic suitability from regional rules.
    /// Precedence: tehsil rule > district rule > province rule.
    /// </summary>
    private void EvaluateLocation(Farm farm, CropRequirement requirement, List<RegionalCropSuitability> rules, CropSuitabilityResultDto result)
    {
        var candidates = rules.Where(r => r.CropCatalogId == requirement.CropCatalogId
            && string.Equals(r.Season, requirement.Season, StringComparison.OrdinalIgnoreCase)).ToList();

        var rule = candidates.FirstOrDefault(r => r.TehsilId == farm.TehsilId)
            ?? candidates.FirstOrDefault(r => r.DistrictId == farm.DistrictId)
            ?? candidates.FirstOrDefault(r => r.DistrictId == null);

        if (rule is null)
        {
            result.Limitations.Add($"No regional suitability rule is currently available for {result.CropName} in this area.");
            return;
        }

        var score = (int)Math.Round(rule.SuitabilityScore / 10.0 * _settings.LocationWeight);
        result.FactorScores.Location = Math.Clamp(score, 0, _settings.LocationWeight);

        var level = rule.TehsilId is not null ? "tehsil" : rule.DistrictId is not null ? "district" : "province";
        if (rule.SuitabilityScore >= 7)
            result.PositiveFactors.Add($"Regional data rates {result.CropName} highly at the {level} level.");
        else if (rule.SuitabilityScore >= 4)
            result.PositiveFactors.Add($"Regional data rates {result.CropName} moderately at the {level} level.");
        else
            result.Limitations.Add($"Regional data rates {result.CropName} low at the {level} level.");
    }

    private void EvaluateSoil(Farm farm, CropRequirement requirement, CropSuitabilityResultDto result)
    {
        if (string.IsNullOrWhiteSpace(requirement.SuitableSoils))
        {
            result.MissingData.Add("No soil compatibility data is available for this crop.");
            return;
        }

        if (string.IsNullOrWhiteSpace(farm.SoilType))
        {
            result.MissingData.Add("Farm soil type is not set, so soil suitability could not be evaluated.");
            return;
        }

        var compatible = requirement.SuitableSoils
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (compatible.Any(s => string.Equals(s, farm.SoilType, StringComparison.OrdinalIgnoreCase)))
        {
            result.FactorScores.Soil = _settings.SoilWeight;
            result.PositiveFactors.Add($"Farm soil '{farm.SoilType}' is compatible with this crop.");
        }
        else
        {
            result.Limitations.Add($"Farm soil '{farm.SoilType}' is not listed among this crop's compatible soils.");
        }
    }

    private void EvaluateWater(Farm farm, CropRequirement requirement, CropSuitabilityResultDto result)
    {
        if (string.IsNullOrWhiteSpace(farm.IrrigationType))
        {
            result.MissingData.Add("Farm irrigation type is not set, so water suitability could not be evaluated.");
            return;
        }

        var noIrrigation = string.Equals(farm.IrrigationType, "Rainfed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(farm.IrrigationType, "None", StringComparison.OrdinalIgnoreCase);

        var waterLevel = requirement.WaterRequirement;
        var satisfied = waterLevel switch
        {
            "High" => !noIrrigation,
            _ => true // Low/Medium needs are achievable with or without irrigation support.
        };

        if (satisfied)
        {
            result.FactorScores.Water = _settings.WaterWeight;
            result.PositiveFactors.Add($"Farm irrigation '{farm.IrrigationType}' can meet this crop's {waterLevel.ToLowerInvariant()} water requirement.");
        }
        else
        {
            result.Limitations.Add($"This crop has a {waterLevel.ToLowerInvariant()} water requirement but the farm is '{farm.IrrigationType}'.");
        }
    }

    /// <summary>
    /// Compares the 7-day forecast temperature envelope with the crop range.
    /// Full points when the forecast sits inside the range, linear taper outside.
    /// </summary>
    private void EvaluateClimate(CropRequirement requirement, ForecastDto? forecast, CropSuitabilityResultDto result)
    {
        if (forecast is null || forecast.Days.Count == 0)
        {
            result.MissingData.Add("Weather data was unavailable, so climate suitability could not be evaluated.");
            return;
        }

        if (requirement.MinTempC is null && requirement.MaxTempC is null)
        {
            result.MissingData.Add("No temperature range data is available for this crop.");
            return;
        }

        var mins = forecast.Days.Where(d => d.TempMin is not null).Select(d => (double)d.TempMin!).ToList();
        var maxs = forecast.Days.Where(d => d.TempMax is not null).Select(d => (double)d.TempMax!).ToList();
        if (mins.Count == 0 || maxs.Count == 0)
        {
            result.MissingData.Add("Weather data was incomplete, so climate suitability could not be evaluated.");
            return;
        }

        var avgMin = mins.Average();
        var avgMax = maxs.Average();

        double factor = 1.0;
        if (requirement.MinTempC is not null && avgMin < (double)requirement.MinTempC)
            factor = Math.Min(factor, Taper((double)requirement.MinTempC - avgMin));
        if (requirement.MaxTempC is not null && avgMax > (double)requirement.MaxTempC)
            factor = Math.Min(factor, Taper(avgMax - (double)requirement.MaxTempC));

        result.FactorScores.Climate = (int)Math.Round(factor * _settings.ClimateWeight);

        if (factor >= 0.999)
            result.PositiveFactors.Add($"Forecast temperatures ({avgMin:F1}-{avgMax:F1} C) are within this crop's suitable range.");
        else if (factor >= 0.5)
            result.Limitations.Add($"Forecast temperatures ({avgMin:F1}-{avgMax:F1} C) are near the edge of this crop's suitable range.");
        else
            result.Limitations.Add($"Forecast temperatures ({avgMin:F1}-{avgMax:F1} C) are outside this crop's suitable range.");
    }

    /// <summary>Linear taper: 10 C outside the range reduces the factor to 0.</summary>
    private static double Taper(double degreesOutside)
        => Math.Max(0.0, 1.0 - degreesOutside / 10.0);

    private string ToLevel(int score)
    {
        if (score >= _settings.HighlySuitableThreshold) return "Highly Suitable";
        if (score >= _settings.SuitableThreshold) return "Suitable";
        if (score >= _settings.ModerateThreshold) return "Moderately Suitable";
        return "Low Suitability";
    }
}
