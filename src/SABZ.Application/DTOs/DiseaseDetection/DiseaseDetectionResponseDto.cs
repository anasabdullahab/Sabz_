namespace SABZ.Application.DTOs.DiseaseDetection;

/// <summary>
/// Response for POST /api/farms/{farmId}/disease-detection (Prompt 6).
/// Clearly separates the AI model output from SABZ curated guidance and
/// never exposes EF entities or provider-specific response objects.
/// </summary>
public class DiseaseDetectionResponseDto
{
    public Guid FarmId { get; set; }
    public Guid? CropId { get; set; }

    /// <summary>Crop context used for the assessment (null when not provided).</summary>
    public DiseaseCropContextDto? CropContext { get; set; }

    public DiseaseImageAssessmentDto ImageAssessment { get; set; } = new();

    /// <summary>Null when the image is not a plant or no AI assessment was produced.</summary>
    public DiseaseAssessmentDto? DiseaseAssessment { get; set; }

    /// <summary>Null when no agricultural advice applies (e.g. non-plant image).</summary>
    public DiseaseAdviceDto? Advice { get; set; }

    public List<string> MissingData { get; set; } = new();

    /// <summary>
    /// True when the AI provider was unavailable (no key / timeout / error) and the
    /// response was built from the local crop knowledge base instead: the farmer
    /// gets the crop's common diseases with symptoms and treatment guidance to
    /// compare visually, clearly marked as NOT an AI image analysis.
    /// </summary>
    public bool IsLocalFallback { get; set; }

    public DiseaseProviderInfoDto Provider { get; set; } = new();

    public DateTime EvaluatedAt { get; set; }

    public string Disclaimer { get; set; } = string.Empty;
}

/// <summary>Contextual crop information supplied by the farmer (validated ownership).</summary>
public class DiseaseCropContextDto
{
    public string CropName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string? GrowthStage { get; set; }
    public DateTime? PlantingDate { get; set; }
    public string? CatalogName { get; set; }
    public string? CatalogCategory { get; set; }
}

/// <summary>Result of image acceptance + plant/leaf relevance checking.</summary>
public class DiseaseImageAssessmentDto
{
    /// <summary>Whether the file passed local image validation.</summary>
    public bool ImageAccepted { get; set; }

    /// <summary>Whether the image appears to show a crop leaf/plant.</summary>
    public bool IsPlantImage { get; set; }

    /// <summary>Confidence (0-1) of the plant-relevance assessment.</summary>
    public double? PlantConfidence { get; set; }

    /// <summary>Human-readable message (rejection reason or relevance explanation).</summary>
    public string? Message { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
    public string? Format { get; set; }
    public bool PossiblyBlurry { get; set; }
}

/// <summary>AI disease assessment - advisory language only, never a diagnosis.</summary>
public class DiseaseAssessmentDto
{
    public bool Detected { get; set; }

    /// <summary>"Likely" / "Possible" / "Uncertain" based on configured thresholds.</summary>
    public string AssessmentLevel { get; set; } = string.Empty;

    public string? Crop { get; set; }
    public string? Disease { get; set; }
    public double? Confidence { get; set; }
    public string? Severity { get; set; }

    /// <summary>Model-provided reasoning/evidence when available.</summary>
    public string? Explanation { get; set; }

    /// <summary>Always "AI model" for provider output.</summary>
    public string AssessmentSource { get; set; } = "AI model";

    /// <summary>
    /// Common diseases of the assessed crop from the local knowledge base,
    /// populated in local fallback mode (AI unavailable) for visual comparison.
    /// Empty in normal AI mode.
    /// </summary>
    public List<string> CommonDiseasesForCrop { get; set; } = new();
}

/// <summary>Agricultural guidance combining AI output with SABZ curated reference data.</summary>
public class DiseaseAdviceDto
{
    public string Summary { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new();
    public List<string> Prevention { get; set; } = new();
    public List<string> Monitoring { get; set; } = new();

    /// <summary>
    /// Labels identifying where the guidance comes from, e.g. "AI model" and/or
    /// "SABZ agricultural knowledge/reference data".
    /// </summary>
    public List<string> AdviceSources { get; set; } = new();
}

/// <summary>Provider transparency block.</summary>
public class DiseaseProviderInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Version { get; set; }

    /// <summary>Whether an API key is configured (false = graceful service-unavailable mode).</summary>
    public bool Configured { get; set; }
}
