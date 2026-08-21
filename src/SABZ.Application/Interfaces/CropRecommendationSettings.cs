namespace SABZ.Application.Interfaces;

/// <summary>
/// Centralized configuration for the next-crop recommendation engine.
/// Bound from appsettings.json section "CropRecommendation".
///
/// Recommendation categories are derived from the Prompt 4 suitability level
/// (one category each: Highly Suitable=3, Suitable=2, Moderately Suitable=1,
/// Low Suitability=0) and then adjusted by the crop-change rule effect.
/// No second independent 0-100 agricultural score is introduced.
/// </summary>
public class CropRecommendationSettings
{
    public const string SectionName = "CropRecommendation";

    /// <summary>
    /// Levels subtracted from the suitability-derived category when the applied
    /// crop-change rule has effect "Caution".
    /// </summary>
    public int CautionLevelAdjustment { get; set; } = 1;

    /// <summary>
    /// Levels subtracted from the suitability-derived category when the applied
    /// crop-change rule has effect "Negative".
    /// </summary>
    public int NegativeLevelAdjustment { get; set; } = 2;
}
