using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Read access to crop suitability reference data (requirements + regional rules).
/// </summary>
public interface ICropSuitabilityDataRepository
{
    /// <summary>All crop requirements with their catalog crop, in a single query.</summary>
    Task<List<CropRequirement>> GetRequirementsAsync(CancellationToken ct = default);

    /// <summary>All regional suitability rules, in a single query.</summary>
    Task<List<RegionalCropSuitability>> GetRegionalRulesAsync(CancellationToken ct = default);
}
