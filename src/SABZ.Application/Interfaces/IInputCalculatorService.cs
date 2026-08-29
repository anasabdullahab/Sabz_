using SABZ.Application.DTOs.InputCalculator;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Precision crop input &amp; dosage calculator (Prompt 16): deterministic
/// farm-area × dosage-rate arithmetic on demand. Pure calculation - no
/// persistence, no AI, no financial or marketplace interaction.
/// </summary>
public interface IInputCalculatorService
{
    /// <summary>
    /// Calculates the required input quantity for the caller's own farm.
    /// The authoritative farm area is read from the Farm record; the client
    /// never supplies an area. Unknown farm → 404, another user's farm → 403.
    /// </summary>
    Task<InputCalculatorResponseDto> CalculateAsync(
        Guid userId, Guid farmId, InputCalculatorRequestDto dto, CancellationToken ct = default);
}
