using Microsoft.Extensions.Options;
using SABZ.Application.DTOs.CropRecommendation;
using SABZ.Application.DTOs.CropSuitability;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;

namespace SABZ.Application.Services.CropRecommendation;

/// <summary>
/// Next-crop recommendation engine (Prompt 5).
///
/// Flow:
/// 1. Reuse the Prompt 4 suitability evaluation (farm ownership, season handling,
///    weather retrieval and scoring all happen there - nothing is duplicated here).
/// 2. Load the farm's actual crop records and determine the previous crop using a
///    documented deterministic rule (see DeterminePreviousCrop).
/// 3. Apply data-driven crop-change rules (CropChangeRule) keyed by catalog category.
/// 4. Map the suitability category to a recommendation category and adjust it by the
///    crop-change effect. Candidates are never silently removed because of history.
///
/// Missing crop history or missing crop-change rules never block the recommendation;
/// they are reported transparently and the result falls back to farm suitability.
/// </summary>
public class CropRecommendationService : ICropRecommendationService
{
    private readonly ICropSuitabilityService _suitabilityService;
    private readonly ICropRepository _cropRepository;
    private readonly ICropChangeRuleRepository _cropChangeRuleRepository;
    private readonly CropRecommendationSettings _settings;

    /// <summary>
    /// Centralized recommendation categories, indexed by internal level 0-3.
    /// This is the single source of truth for farmer-facing recommendation labels.
    /// </summary>
    private static readonly string[] RecommendationLevels =
    {
        "Not Recommended",   // 0
        "Consider",          // 1
        "Recommended",       // 2
        "Highly Recommended"  // 3
    };

    /// <summary>Prompt 4 suitability category mapped to the internal level it starts from.</summary>
    private static readonly Dictionary<string, int> SuitabilityBaseLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Highly Suitable"] = 3,
        ["Suitable"] = 2,
        ["Moderately Suitable"] = 1,
        ["Low Suitability"] = 0
    };

    private const string EffectPositive = "Positive";
    private const string EffectCaution = "Caution";
    private const string EffectNegative = "Negative";

    public CropRecommendationService(
        ICropSuitabilityService suitabilityService,
        ICropRepository cropRepository,
        ICropChangeRuleRepository cropChangeRuleRepository,
        IOptions<CropRecommendationSettings> settings)
    {
        _suitabilityService = suitabilityService;
        _cropRepository = cropRepository;
        _cropChangeRuleRepository = cropChangeRuleRepository;
        _settings = settings.Value;
    }

    public async Task<CropRecommendationResponseDto> RecommendAsync(Guid userId, Guid farmId, string? season, CancellationToken ct = default)
    {
        // Prompt 4 evaluation performs farm lookup, ownership check, season
        // validation/auto-detection and weather retrieval (at most once).
        var suitability = await _suitabilityService.EvaluateAsync(userId, farmId, season, ct);

        // Single history query, single rules query - both reused across all candidates.
        var history = await _cropRepository.GetHistoryByFarmIdAsync(farmId);
        var previousCrop = DeterminePreviousCrop(history);

        var rules = previousCrop?.CropCatalog?.Category is not null
            ? await _cropChangeRuleRepository.GetActiveRulesAsync(ct)
            : new List<CropChangeRule>();

        var recommendations = suitability.Crops
            .Select(crop => BuildRecommendation(crop, previousCrop, rules))
            .OrderByDescending(r => RecommendationRank(r.Recommendation))
            .ThenByDescending(r => r.SuitabilityScore)
            .ThenBy(r => r.CropName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CropRecommendationResponseDto
        {
            FarmId = suitability.FarmId,
            Location = suitability.Location,
            EvaluationSeason = suitability.EvaluationSeason,
            SeasonSource = suitability.SeasonSource,
            EvaluatedAt = DateTime.UtcNow,
            CropHistory = BuildHistorySummary(previousCrop, history),
            Recommendations = recommendations
        };
    }

    /// <summary>
    /// Documented previous-crop rule:
    /// The previous crop is the most recent COMPLETED crop cycle on the farm -
    /// i.e. the newest record (by planting date, falling back to record creation
    /// date) whose status is "Harvested" or "Failed". Records with status "Active"
    /// represent the currently growing crop, and "Planned" records are excluded,
    /// so neither can be mistaken for a previous crop.
    /// Returns null when no completed record exists - a previous crop is never invented.
    /// </summary>
    private static Crop? DeterminePreviousCrop(List<Crop> historyRecords)
    {
        return historyRecords
            .Where(c => string.Equals(c.Status, "Harvested", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(c.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(); // already ordered most-recent first by the repository
    }

    private CropRecommendationItemDto BuildRecommendation(
        CropSuitabilityResultDto crop,
        Crop? previousCrop,
        List<CropChangeRule> rules)
    {
        var baseLevel = SuitabilityBaseLevels.TryGetValue(crop.SuitabilityLevel, out var lvl) ? lvl : 0;

        var previousCategory = previousCrop?.CropCatalog?.Category;
        var candidateCategory = crop.CropCatalogId > 0 ? GetCandidateCategory(crop) : null;

        // Find the applicable crop-change rule (case-insensitive category match).
        CropChangeRule? rule = null;
        if (previousCategory is not null && candidateCategory is not null)
        {
            rule = rules.FirstOrDefault(r =>
                string.Equals(r.PreviousCategory, previousCategory, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.NextCategory, candidateCategory, StringComparison.OrdinalIgnoreCase));
        }

        var adjustment = rule?.Effect switch
        {
            EffectCaution => _settings.CautionLevelAdjustment,
            EffectNegative => _settings.NegativeLevelAdjustment,
            _ => 0
        };

        var finalLevel = Math.Clamp(baseLevel - adjustment, 0, RecommendationLevels.Length - 1);
        var recommendation = RecommendationLevels[finalLevel];

        var item = new CropRecommendationItemDto
        {
            CropId = crop.CropCatalogId,
            CropName = crop.CropName,
            FarmSuitability = crop.SuitabilityLevel,
            Recommendation = recommendation,
            SuitabilityScore = crop.SuitabilityScore,
            HistoryConsideration = rule?.Effect,
            PositiveFactors = new List<string>(crop.PositiveFactors),
            Limitations = new List<string>(crop.Limitations),
            MissingData = new List<string>(crop.MissingData),
            Explanation = BuildExplanation(crop, previousCrop, rule, finalLevel)
        };

        if (rule is not null)
        {
            if (rule.Effect == EffectPositive)
                item.PositiveFactors.Add($"Crop history: {rule.Explanation}");
            else
                item.Limitations.Add($"Crop history: {rule.Explanation}");
        }
        else if (previousCrop is not null)
        {
            item.MissingData.Add("Crop-change information is not available for this crop, so crop history could not be evaluated for it.");
        }

        return item;
    }

    /// <summary>
    /// Catalog category of a suitability candidate. Suitability results only carry the
    /// catalog id, so the category is looked up through the crop-change rules' matching
    /// categories via the repository-free helper below (category strings come from the
    /// catalog seed data and are stable reference values).
    /// </summary>
    private static string? GetCandidateCategory(CropSuitabilityResultDto crop) => crop.CandidateCategory;

    private string BuildExplanation(
        CropSuitabilityResultDto crop,
        Crop? previousCrop,
        CropChangeRule? rule,
        int finalLevel)
    {
        var suitabilityPhrase = crop.SuitabilityLevel == "Low Suitability"
            ? "have low suitability"
            : $"are {crop.SuitabilityLevel.ToLowerInvariant()}";

        var text = $"Your farm conditions {suitabilityPhrase} for {crop.CropName.ToLowerInvariant()}";

        if (previousCrop is null)
        {
            return text + ". Crop-history information is not available, so this recommendation is based on the available farm suitability information.";
        }

        var previousName = previousCrop.CropCatalog?.Name ?? previousCrop.CropName;

        if (rule is null)
        {
            return text + $", but crop-change information is not available for {previousName} followed by this crop, so the recommendation is based on farm suitability.";
        }

        var historyPhrase = rule.Effect switch
        {
            EffectPositive => "and the available crop-history information supports considering it as your next crop",
            EffectCaution => "but the available crop-history information suggests considering other options",
            EffectNegative => "but the available crop-history information advises against this crop change",
            _ => "and crop-history information was reviewed"
        };

        return text + ", " + historyPhrase + ".";
    }

    private static CropHistorySummaryDto BuildHistorySummary(Crop? previousCrop, List<Crop> history)
    {
        var completedRecords = history
            .Count(c => string.Equals(c.Status, "Harvested", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(c.Status, "Failed", StringComparison.OrdinalIgnoreCase));

        if (previousCrop is null)
        {
            return new CropHistorySummaryDto
            {
                Available = false,
                UsableRecordCount = completedRecords,
                HistoryNote = "Crop-history information is not available, so the recommendation is based on the available farm suitability information."
            };
        }

        return new CropHistorySummaryDto
        {
            Available = true,
            PreviousCropName = previousCrop.CropCatalog?.Name ?? previousCrop.CropName,
            PreviousCropCategory = previousCrop.CropCatalog?.Category ?? string.Empty,
            PreviousCropSeason = previousCrop.Season,
            UsableRecordCount = completedRecords,
            HistoryNote = $"Previous crop determined from actual crop records (most recent completed crop cycle): {previousCrop.CropCatalog?.Name ?? previousCrop.CropName}."
        };
    }

    private static int RecommendationRank(string recommendation) =>
        Array.IndexOf(RecommendationLevels, recommendation);
}
