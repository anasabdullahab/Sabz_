namespace SABZ.Application.Interfaces;

/// <summary>
/// Centralized configuration for the crop suitability scoring engine.
/// Bound from appsettings.json section "CropSuitability".
///
/// Factor weights must sum to 100. Level thresholds map the total score
/// into suitability categories. Season detection uses the configured
/// Kharif month range; months outside it are treated as Rabi.
/// </summary>
public class CropSuitabilitySettings
{
    public const string SectionName = "CropSuitability";

    // --- Factor weights (must total 100) ---

    /// <summary>Points available for geographic suitability (province/district/tehsil rules).</summary>
    public int LocationWeight { get; set; } = 25;

    /// <summary>Points available for weather/climate match against crop temperature range.</summary>
    public int ClimateWeight { get; set; } = 25;

    /// <summary>Points available for farm soil compatibility with the crop.</summary>
    public int SoilWeight { get; set; } = 20;

    /// <summary>Points available for irrigation satisfying the crop water requirement.</summary>
    public int WaterWeight { get; set; } = 15;

    /// <summary>Points available for the crop matching the evaluation season.</summary>
    public int SeasonWeight { get; set; } = 15;

    // --- Suitability level thresholds ---

    /// <summary>Score >= this is "Highly Suitable".</summary>
    public int HighlySuitableThreshold { get; set; } = 80;

    /// <summary>Score >= this is "Suitable".</summary>
    public int SuitableThreshold { get; set; } = 60;

    /// <summary>Score >= this is "Moderately Suitable"; below is "Low Suitability".</summary>
    public int ModerateThreshold { get; set; } = 40;

    // --- Season auto-detection ---

    /// <summary>First month (1-12) of the Kharif season.</summary>
    public int KharifStartMonth { get; set; } = 4;

    /// <summary>Last month (1-12) of the Kharif season.</summary>
    public int KharifEndMonth { get; set; } = 9;
}
