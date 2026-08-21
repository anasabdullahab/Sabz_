namespace SABZ.Application.DTOs.CropSuitability;

/// <summary>
/// Suitability evaluation result for a single crop.
/// </summary>
public class CropSuitabilityResultDto
{
    public int CropCatalogId { get; set; }
    public string CropName { get; set; } = string.Empty;

    /// <summary>Total SABZ suitability score, 0-100.</summary>
    public int SuitabilityScore { get; set; }

    /// <summary>Category derived from configured thresholds (e.g. "Highly Suitable").</summary>
    public string SuitabilityLevel { get; set; } = string.Empty;

    public FactorScoresDto FactorScores { get; set; } = new();

    /// <summary>Factors that positively contributed to the score.</summary>
    public List<string> PositiveFactors { get; set; } = new();

    /// <summary>Factors that reduced the score.</summary>
    public List<string> Limitations { get; set; } = new();

    /// <summary>Factors that could not be evaluated because data is missing.</summary>
    public List<string> MissingData { get; set; } = new();
}

/// <summary>Points awarded per scoring factor (each capped at its configured weight).</summary>
public class FactorScoresDto
{
    public int Location { get; set; }
    public int Climate { get; set; }
    public int Soil { get; set; }
    public int Water { get; set; }
    public int Season { get; set; }
}
