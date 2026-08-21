using SABZ.Application.DTOs.CropSuitability;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Evaluates how suitable catalog crops are for a specific farm using
/// farm data, location rules, crop requirement data and (optionally) weather.
/// </summary>
public interface ICropSuitabilityService
{
    /// <summary>
    /// Evaluate crop suitability for an owned farm.
    /// </summary>
    /// <param name="userId">Authenticated user (from JWT) - must own the farm.</param>
    /// <param name="farmId">Farm to evaluate.</param>
    /// <param name="season">Optional season ("Rabi"/"Kharif"); auto-detected when null.</param>
    Task<CropSuitabilityResponseDto> EvaluateAsync(Guid userId, Guid farmId, string? season, CancellationToken ct = default);
}
