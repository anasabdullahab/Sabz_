using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Read access to data-driven crop-change (rotation) reference rules.
/// </summary>
public interface ICropChangeRuleRepository
{
    /// <summary>All active crop-change rules; loaded once and reused across candidates.</summary>
    Task<List<CropChangeRule>> GetActiveRulesAsync(CancellationToken ct = default);
}
