namespace SABZ.Application.Interfaces;

/// <summary>
/// Structured result returned by an AI disease-detection provider.
/// Provider-specific response objects must never leave Infrastructure;
/// this DTO is the only shape the Application/API layers see.
/// </summary>
public sealed class PlantDiseaseDetectionResult
{
    // Plant/leaf relevance assessment.
    public bool IsPlantImage { get; init; }
    public double PlantConfidence { get; init; }
    public string? PlantReason { get; init; }

    // Disease assessment (may be absent).
    public string? DetectedCrop { get; init; }
    public bool DiseaseDetected { get; init; }
    public string? DiseaseName { get; init; }
    public double DiseaseConfidence { get; init; }
    public string? Severity { get; init; }
    public string? Explanation { get; init; }

    // Provider transparency.
    public required string ProviderName { get; init; }
    public required string ModelName { get; init; }
    public string? ModelVersion { get; init; }
}
