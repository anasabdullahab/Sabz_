using SABZ.Application.DTOs.CropSuitability;

namespace SABZ.Application.DTOs.CropRecommendation;

/// <summary>
/// Top-level next-crop recommendation response for a farm.
/// Built on top of the Prompt 4 suitability evaluation plus crop-history analysis.
/// </summary>
public class CropRecommendationResponseDto
{
    public Guid FarmId { get; set; }
    public FarmLocationDto Location { get; set; } = new();

    /// <summary>Season used for the evaluation ("Rabi" or "Kharif").</summary>
    public string EvaluationSeason { get; set; } = string.Empty;

    /// <summary>How the season was chosen: "ClientProvided" or "AutoDetected".</summary>
    public string SeasonSource { get; set; } = string.Empty;

    public DateTime EvaluatedAt { get; set; }

    /// <summary>Summary of the farm's crop history used by the recommendation.</summary>
    public CropHistorySummaryDto CropHistory { get; set; } = new();

    /// <summary>Candidate crops ordered by recommendation quality (best first).</summary>
    public List<CropRecommendationItemDto> Recommendations { get; set; } = new();

    /// <summary>
    /// Recommendations combine SABZ suitability evaluation with limited crop-history
    /// guidance - they are not guaranteed agricultural outcomes.
    /// </summary>
    public string Disclaimer { get; set; } =
        "SABZ recommendation based on farm suitability and available crop history. Not a guaranteed agricultural outcome.";
}

/// <summary>
/// What the system could determine about the farm's previously grown crops.
/// When <see cref="Available"/> is false, no previous crop was invented and the
/// recommendation is based purely on farm suitability.
/// </summary>
public class CropHistorySummaryDto
{
    /// <summary>Whether a reliable previous crop could be determined from actual records.</summary>
    public bool Available { get; set; }

    /// <summary>Name of the determined previous crop (empty when unavailable).</summary>
    public string PreviousCropName { get; set; } = string.Empty;

    /// <summary>CropCatalog category of the previous crop (empty when unknown/unlinked).</summary>
    public string PreviousCropCategory { get; set; } = string.Empty;

    /// <summary>Season recorded on the previous crop record (empty when unavailable).</summary>
    public string PreviousCropSeason { get; set; } = string.Empty;

    /// <summary>Number of usable historical crop records found on the farm.</summary>
    public int UsableRecordCount { get; set; }

    /// <summary>Human-readable note about how history was (or was not) used.</summary>
    public string HistoryNote { get; set; } = string.Empty;
}
