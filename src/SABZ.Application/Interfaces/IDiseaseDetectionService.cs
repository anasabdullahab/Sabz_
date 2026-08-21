using SABZ.Application.DTOs.DiseaseDetection;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Orchestrates the Prompt 6 pipeline:
/// image validation -> plant/leaf relevance -> disease identification ->
/// confidence handling -> cautious agricultural advice.
/// </summary>
public interface IDiseaseDetectionService
{
    /// <summary>
    /// Analyse an uploaded crop photograph for an owned farm.
    /// userId must come from the JWT - never from the request body.
    /// </summary>
    Task<DiseaseDetectionResponseDto> DetectAsync(
        Guid userId,
        Guid farmId,
        byte[] imageBytes,
        string? contentType,
        string? fileName,
        Guid? cropId,
        string? notes,
        CancellationToken ct = default);
}
