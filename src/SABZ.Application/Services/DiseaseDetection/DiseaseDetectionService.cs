using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SABZ.Application.DTOs.DiseaseDetection;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.DiseaseDetection;

/// <summary>
/// Prompt 6 pipeline: image validation -> plant/leaf relevance ->
/// disease identification -> confidence handling -> cautious advice.
///
/// Ownership reuses the existing pattern (JWT user id only, 404/403).
/// The AI provider is called at most once per request and is never
/// invoked for images that fail validation or the relevance check.
/// Missing context is reported, never invented.
/// </summary>
public class DiseaseDetectionService : IDiseaseDetectionService
{
    private const string NotPlantMessage =
        "The uploaded image does not appear to show a crop or leaf. Please upload a clear photograph of a crop leaf or plant.";

    private const string Disclaimer =
        "SABZ AI assessments are advisory only and not a laboratory diagnosis. " +
        "For confirmed diagnosis and any treatment decisions, consult a local agricultural expert or approved product labels.";

    private readonly IFarmRepository _farmRepository;
    private readonly ICropRepository _cropRepository;
    private readonly IWeatherService _weatherService;
    private readonly IImageValidator _imageValidator;
    private readonly IPlantDiseaseDetectionProvider _provider;
    private readonly IDiseaseInformationRepository _diseaseInformation;
    private readonly DiseaseDetectionSettings _settings;
    private readonly ILogger<DiseaseDetectionService> _logger;

    public DiseaseDetectionService(
        IFarmRepository farmRepository,
        ICropRepository cropRepository,
        IWeatherService weatherService,
        IImageValidator imageValidator,
        IPlantDiseaseDetectionProvider provider,
        IDiseaseInformationRepository diseaseInformation,
        IOptions<DiseaseDetectionSettings> settings,
        ILogger<DiseaseDetectionService> logger)
    {
        _farmRepository = farmRepository;
        _cropRepository = cropRepository;
        _weatherService = weatherService;
        _imageValidator = imageValidator;
        _provider = provider;
        _diseaseInformation = diseaseInformation;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<DiseaseDetectionResponseDto> DetectAsync(
        Guid userId,
        Guid farmId,
        byte[] imageBytes,
        string? contentType,
        string? fileName,
        Guid? cropId,
        string? notes,
        CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId);
        var crop = await GetOwnedCropAsync(farm, cropId);

        var response = new DiseaseDetectionResponseDto
        {
            FarmId = farm.Id,
            CropId = crop?.Id,
            CropContext = BuildCropContext(crop),
            Provider = new DiseaseProviderInfoDto
            {
                Name = _provider.ProviderName,
                Model = _provider.ModelName,
                Version = _provider.ModelVersion,
                Configured = _provider.IsConfigured
            },
            EvaluatedAt = DateTime.UtcNow,
            Disclaimer = Disclaimer
        };

        if (crop is null)
            response.MissingData.Add("No crop context was provided. Disease detection still works without it, but crop details improve accuracy.");

        // Stage 1 - local image quality validation (no AI call for invalid files).
        var validation = _imageValidator.Validate(imageBytes, contentType, fileName);
        response.ImageAssessment.ImageAccepted = validation.IsValid;
        response.ImageAssessment.Width = validation.Width;
        response.ImageAssessment.Height = validation.Height;
        response.ImageAssessment.Format = validation.Format;
        response.ImageAssessment.PossiblyBlurry = validation.PossiblyBlurry;

        if (!validation.IsValid)
            throw new ValidationException(validation.Error ?? "The uploaded file is not a valid image.");

        if (validation.PossiblyBlurry)
            response.MissingData.Add("The uploaded image appears possibly blurry; a sharper photograph will improve assessment accuracy.");

        if (!farm.Latitude.HasValue || !farm.Longitude.HasValue)
            response.MissingData.Add("This farm has no GPS coordinates, so weather context was not included in the assessment.");

        // Stage 2 - provider not configured: graceful service-unavailable, never a fake result.
        if (!_provider.IsConfigured)
            throw new DiseaseProviderException(
                "The AI disease-detection provider is not configured. " +
                "Set DiseaseDetection:ApiKey in local configuration to enable live analysis.");

        // Optional weather context via the existing abstraction (single cached call).
        var weatherHint = await TryGetWeatherContextAsync(userId, farmId, farm, ct);
        if (weatherHint is null && farm.Latitude.HasValue)
            response.MissingData.Add("Weather data was temporarily unavailable and was not included in the assessment.");

        // Stage 3 - exactly one AI call performs relevance + disease assessment.
        var result = await _provider.DetectAsync(new PlantDiseaseDetectionRequest
        {
            ImageBytes = imageBytes,
            ImageMimeType = contentType ?? "application/octet-stream",
            CropNameHint = crop?.CropName ?? crop?.CropCatalog?.Name,
            CropCategoryHint = crop?.CropCatalog?.Category,
            SeasonHint = crop?.Season,
            GrowthStageHint = crop?.GrowthStage,
            WeatherContextHint = weatherHint,
            FarmerNotes = notes
        }, ct);

        // Stage 4 - plant/leaf relevance gate: no disease classification on unrelated images.
        response.ImageAssessment.IsPlantImage = result.IsPlantImage;
        response.ImageAssessment.PlantConfidence = result.PlantConfidence;
        response.ImageAssessment.Message = result.PlantReason;

        if (!result.IsPlantImage || result.PlantConfidence < _settings.PlantConfidenceThreshold)
        {
            response.ImageAssessment.IsPlantImage = false;
            response.ImageAssessment.Message = NotPlantMessage;
            return response;
        }

        // Stage 5 - confidence-based disease assessment (never a confirmed diagnosis).
        response.DiseaseAssessment = BuildAssessment(result, crop);

        // Stage 6 - cautious, data-driven agricultural advice.
        response.Advice = await BuildAdviceAsync(result, response.DiseaseAssessment, crop, weatherHint, response.MissingData, ct);

        return response;
    }

    // ------------------------------------------------------------------
    //  Ownership (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return farm;
    }

    private async Task<Crop?> GetOwnedCropAsync(Farm farm, Guid? cropId)
    {
        if (cropId is null)
            return null;

        var crop = await _cropRepository.GetByIdAsync(cropId.Value)
            ?? throw new NotFoundException("Crop not found.");

        if (crop.FarmId != farm.Id)
            throw new ForbiddenException("The specified crop does not belong to this farm.");

        return crop;
    }

    // ------------------------------------------------------------------
    //  Context building
    // ------------------------------------------------------------------

    private static DiseaseCropContextDto? BuildCropContext(Crop? crop)
    {
        if (crop is null)
            return null;

        return new DiseaseCropContextDto
        {
            CropName = crop.CropName,
            Season = crop.Season,
            GrowthStage = crop.GrowthStage,
            PlantingDate = crop.PlantingDate,
            CatalogName = crop.CropCatalog?.Name,
            CatalogCategory = crop.CropCatalog?.Category
        };
    }

    /// <summary>
    /// Reuses the existing weather abstraction (with caching). Failures degrade
    /// gracefully - disease detection never depends on weather being available.
    /// </summary>
    private async Task<string?> TryGetWeatherContextAsync(Guid userId, Guid farmId, Farm farm, CancellationToken ct)
    {
        if (!farm.Latitude.HasValue || !farm.Longitude.HasValue)
            return null;

        try
        {
            var response = await _weatherService.GetForecastAsync(userId, farmId, ct);
            var days = response.Forecast?.Days;
            if (days is null || days.Count == 0)
                return null;

            var mins = days.Where(d => d.TempMin is not null).Select(d => (double)d.TempMin!).ToList();
            var maxs = days.Where(d => d.TempMax is not null).Select(d => (double)d.TempMax!).ToList();
            if (mins.Count == 0 || maxs.Count == 0)
                return null;

            return $"Forecast average temperatures {mins.Average():F1}-{maxs.Average():F1} C over the next days.";
        }
        catch (Exception ex) when (ex is not ForbiddenException and not NotFoundException)
        {
            _logger.LogWarning(ex, "Weather unavailable during disease detection for farm {FarmId}.", farmId);
            return null;
        }
    }

    // ------------------------------------------------------------------
    //  Confidence handling
    // ------------------------------------------------------------------

    private DiseaseAssessmentDto BuildAssessment(PlantDiseaseDetectionResult result, Crop? crop)
    {
        var assessment = new DiseaseAssessmentDto
        {
            Crop = result.DetectedCrop ?? crop?.CropCatalog?.Name ?? crop?.CropName,
            Confidence = result.DiseaseConfidence,
            Severity = result.Severity,
            Explanation = result.Explanation
        };

        if (!result.DiseaseDetected
            || string.IsNullOrWhiteSpace(result.DiseaseName)
            || result.DiseaseConfidence < _settings.MinimumDiseaseConfidence)
        {
            // Low confidence: never identify a disease.
            assessment.Detected = false;
            assessment.AssessmentLevel = "Uncertain";
            assessment.Explanation ??=
                "The AI could not identify a disease with sufficient confidence in this photograph.";
            return assessment;
        }

        assessment.Detected = true;
        assessment.Disease = result.DiseaseName;
        assessment.AssessmentLevel = result.DiseaseConfidence >= _settings.HighConfidenceThreshold
            ? "Likely"
            : "Possible";
        return assessment;
    }

    // ------------------------------------------------------------------
    //  Advice (curated reference data first, cautious generic guidance otherwise)
    // ------------------------------------------------------------------

    private async Task<DiseaseAdviceDto> BuildAdviceAsync(
        PlantDiseaseDetectionResult result,
        DiseaseAssessmentDto assessment,
        Crop? crop,
        string? weatherHint,
        List<string> missingData,
        CancellationToken ct)
    {
        var advice = new DiseaseAdviceDto();
        advice.AdviceSources.Add("AI model");

        var weatherNote = weatherHint is null
            ? null
            : $"Weather context: {weatherHint} Warm, humid conditions can favour some fungal diseases - pay extra attention after rain.";

        if (!assessment.Detected)
        {
            // Uncertain result: request a clearer photograph, give monitoring guidance only.
            advice.Summary =
                "The image appears to show a plant, but the AI could not identify a disease with sufficient confidence. " +
                "No disease is being claimed. Please take another clear, close-up photograph of the affected leaf in good daylight.";
            advice.RecommendedActions.AddRange(new[]
            {
                "Take another clear, close-up photograph of the affected leaf or plant in good daylight.",
                "Avoid applying any treatment based solely on an uncertain AI assessment.",
                "If symptoms spread or worsen, contact a local agricultural extension office or expert."
            });
            advice.Monitoring.AddRange(new[]
            {
                "Inspect the affected and neighbouring plants every few days for spreading spots, yellowing or wilting.",
                "Note when symptoms first appeared and whether they are spreading."
            });
            if (weatherNote is not null)
                advice.Monitoring.Add(weatherNote);
            return advice;
        }

        var level = assessment.AssessmentLevel == "Likely" ? "likely" : "possibly";
        advice.Summary =
            $"AI assessment indicates a {level} issue with '{assessment.Disease}'" +
            (assessment.Crop is null ? string.Empty : $" on {assessment.Crop}") +
            $" (confidence {assessment.Confidence:P0}). This is an advisory assessment, not a confirmed diagnosis.";

        var curated = await MatchCuratedGuidanceAsync(assessment.Disease, crop, ct);
        if (curated is not null)
        {
            advice.AdviceSources.Add("SABZ agricultural knowledge/reference data");
            advice.RecommendedActions.AddRange(SplitList(curated.RecommendedActions));
            advice.Prevention.AddRange(SplitList(curated.Prevention));
            advice.Monitoring.AddRange(SplitList(curated.Monitoring));
            advice.Monitoring.Add($"Observed symptom reference: {curated.Symptoms}");
        }
        else
        {
            missingData.Add(
                $"No curated SABZ guidance is available yet for '{assessment.Disease}'; only general cautious advice is provided.");
            advice.RecommendedActions.AddRange(new[]
            {
                "Take another clear, close-up photograph in good daylight to confirm the assessment.",
                "Remove or isolate severely affected leaves/plant parts where practical.",
                "Consult a local agricultural extension office or an approved product label before applying any treatment."
            });
            advice.Prevention.AddRange(new[]
            {
                "Avoid overhead irrigation late in the day so leaves dry quickly.",
                "Keep tools clean when pruning or handling affected plants."
            });
            advice.Monitoring.AddRange(new[]
            {
                "Monitor the affected area every few days and record whether symptoms spread.",
                "Seek expert help promptly if symptoms worsen or spread to healthy plants."
            });
        }

        // Always-end guidance: no invented chemical dosages.
        advice.RecommendedActions.Add(
            "For any chemical or pesticide use, follow only an approved product label or a local agricultural expert - SABZ does not prescribe dosages.");
        if (weatherNote is not null)
            advice.Monitoring.Add(weatherNote);

        return advice;
    }

    /// <summary>
    /// Matches the AI disease name against curated SABZ guidance for the crop
    /// (exact name first, then bidirectional containment). No giant switch
    /// statements - guidance is data-driven and maintainable in the database.
    /// </summary>
    private async Task<DiseaseInformation?> MatchCuratedGuidanceAsync(string? disease, Crop? crop, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disease))
            return null;

        var candidates = await _diseaseInformation.GetActiveForCropAsync(crop?.CropCatalogId, ct);
        if (candidates.Count == 0)
            return null;

        var normalized = disease.Trim();
        return candidates.FirstOrDefault(d => string.Equals(d.DiseaseName, normalized, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(d => normalized.Contains(d.DiseaseName, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(d => d.DiseaseName.Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> SplitList(string semicolonSeparated)
        => semicolonSeparated
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
