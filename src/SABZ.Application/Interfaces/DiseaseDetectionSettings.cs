namespace SABZ.Application.Interfaces;

/// <summary>
/// Configuration for the AI crop disease detection pipeline (Prompt 6).
/// All confidence thresholds, image limits and provider credentials live
/// here - no magic numbers scattered through the code.
/// The API key must only ever be provided via local configuration or
/// environment variables, never committed to source control.
/// </summary>
public sealed class DiseaseDetectionSettings
{
    public const string SectionName = "DiseaseDetection";

    /// <summary>Provider identifier (informational; implementation selected via DI).</summary>
    public string Provider { get; set; } = "DashScope";

    /// <summary>Vision model used for plant-relevance + disease assessment.</summary>
    public string Model { get; set; } = "qwen-vl-max";

    /// <summary>OpenAI-compatible endpoint of the vision provider.</summary>
    public string ApiBaseUrl { get; set; } = "https://dashscope-intl.aliyuncs.com/compatible-mode/v1";

    /// <summary>Provider API key. Empty = provider not configured (graceful 502).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Minimum confidence that the image shows a plant/leaf before disease analysis proceeds.</summary>
    public double PlantConfidenceThreshold { get; set; } = 0.6;

    /// <summary>Disease confidence at or above which a stronger "Likely" assessment is given.</summary>
    public double HighConfidenceThreshold { get; set; } = 0.7;

    /// <summary>Below this disease confidence no disease is identified at all.</summary>
    public double MinimumDiseaseConfidence { get; set; } = 0.4;

    public int MaxImageSizeMb { get; set; } = 10;

    /// <summary>Allowed image MIME types (validated against content, not just the header).</summary>
    public string[] AllowedImageTypes { get; set; } = { "image/jpeg", "image/png", "image/webp" };

    public int MinImageWidth { get; set; } = 128;
    public int MinImageHeight { get; set; } = 128;
    public int MaxImageWidth { get; set; } = 6000;
    public int MaxImageHeight { get; set; } = 6000;

    /// <summary>
    /// Laplacian variance below which the (downscaled) image is reported as
    /// possibly blurry. Reporting only - never a hard rejection.
    /// </summary>
    public double BlurVarianceThreshold { get; set; } = 40;
}
