using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface ICropMonitoringRuleRepository
{
    /// <summary>
    /// Active scheduled-trigger rules applicable to a crop catalog entry:
    /// catalog-specific rules plus general rules (CropCatalogId null).
    /// </summary>
    Task<List<CropMonitoringRule>> GetActiveScheduledForCropAsync(int? cropCatalogId, CancellationToken ct = default);
}
