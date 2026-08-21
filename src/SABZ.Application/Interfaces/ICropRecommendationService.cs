using SABZ.Application.DTOs.CropRecommendation;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Answers: "Considering this farm's conditions and the crops previously grown on it,
/// which crops should the farmer consider growing next?"
///
/// Reuses the Prompt 4 suitability evaluation (no duplicated scoring engine) and adds
/// crop-history / crop-change guidance on top of it.
/// </summary>
public interface ICropRecommendationService
{
    /// <summary>
    /// Evaluate next-crop recommendations for an owned farm.
    /// </summary>
    /// <param name="userId">Authenticated user (from JWT) - must own the farm.</param>
    /// <param name="farmId">Farm to evaluate.</param>
    /// <param name="season">Optional season ("Rabi"/"Kharif"); auto-detected when null.</param>
    Task<CropRecommendationResponseDto> RecommendAsync(Guid userId, Guid farmId, string? season, CancellationToken ct = default);
}
