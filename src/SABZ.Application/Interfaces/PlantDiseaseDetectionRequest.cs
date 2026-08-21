namespace SABZ.Application.Interfaces;

/// <summary>
/// Input for the external AI disease-detection provider.
/// The Application layer never touches HTTP SDKs, provider classes or
/// API keys directly - only this neutral contract.
/// </summary>
public sealed class PlantDiseaseDetectionRequest
{
    /// <summary>Validated image bytes (already checked locally).</summary>
    public required byte[] ImageBytes { get; init; }

    /// <summary>Verified MIME type of the image content.</summary>
    public required string ImageMimeType { get; init; }

    // Optional context hints - all nullable, never invented by SABZ.
    public string? CropNameHint { get; init; }
    public string? CropCategoryHint { get; init; }
    public string? SeasonHint { get; init; }
    public string? GrowthStageHint { get; init; }
    public string? WeatherContextHint { get; init; }
    public string? FarmerNotes { get; init; }
}
