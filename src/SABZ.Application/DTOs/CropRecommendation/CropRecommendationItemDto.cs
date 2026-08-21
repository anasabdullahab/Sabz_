namespace SABZ.Application.DTOs.CropRecommendation;

/// <summary>
/// Recommendation result for a single candidate crop.
/// </summary>
public class CropRecommendationItemDto
{
    public int CropId { get; set; }
    public string CropName { get; set; } = string.Empty;

    /// <summary>
    /// Farmer-facing Prompt 4 suitability category:
    /// "Highly Suitable", "Suitable", "Moderately Suitable" or "Low Suitability".
    /// </summary>
    public string FarmSuitability { get; set; } = string.Empty;

    /// <summary>
    /// Farmer-facing recommendation category:
    /// "Highly Recommended", "Recommended", "Consider" or "Not Recommended".
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;

    /// <summary>
    /// Internal Prompt 4 suitability score (0-100), exposed for ranking transparency.
    /// It is an evaluation score, not a scientific probability.
    /// </summary>
    public int SuitabilityScore { get; set; }

    /// <summary>Crop-history effect applied to this candidate ("Positive"/"Caution"/"Negative"), or null when no rule applied.</summary>
    public string? HistoryConsideration { get; set; }

    /// <summary>Farmer-friendly explanation of the recommendation.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Factors that positively contributed to the recommendation.</summary>
    public List<string> PositiveFactors { get; set; } = new();

    /// <summary>Factors that reduced the recommendation.</summary>
    public List<string> Limitations { get; set; } = new();

    /// <summary>Data that could not be evaluated because it is missing.</summary>
    public List<string> MissingData { get; set; } = new();
}
