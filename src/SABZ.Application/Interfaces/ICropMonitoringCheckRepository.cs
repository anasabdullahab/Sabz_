using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface ICropMonitoringCheckRepository
{
    /// <summary>Checks for one crop including crop/farm context, ordered by scheduled date.</summary>
    Task<List<CropMonitoringCheck>> GetByCropIdAsync(Guid cropId, CancellationToken ct = default);

    /// <summary>Checks across all farms of a user, ordered by scheduled date.</summary>
    Task<List<CropMonitoringCheck>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Check with crop/farm context loaded (for ownership verification).</summary>
    Task<CropMonitoringCheck?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Rule ids that already have a check for the crop (idempotent generation).</summary>
    Task<HashSet<int>> GetExistingRuleIdsAsync(Guid cropId, CancellationToken ct = default);

    Task AddAsync(CropMonitoringCheck check, CancellationToken ct = default);
    void Update(CropMonitoringCheck check);
    Task SaveChangesAsync(CancellationToken ct = default);
}
