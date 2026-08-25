using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Lightweight lifecycle row of one monitoring check for the Prompt 11
/// recorded-activity summary: persisted status plus the moment the farmer
/// completed or skipped it. Projected in SQL; full check entities are never
/// loaded for aggregation.
/// </summary>
public sealed record MonitoringCheckEventRow(MonitoringCheckStatus Status, DateTime? CompletedAt, DateTime? SkippedAt);

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

    /// <summary>
    /// Lifecycle rows of every check of one farm, any status (Prompt 11
    /// recorded-activity summary). Ownership is enforced by the caller via
    /// farm ownership; AsNoTracking read.
    /// </summary>
    Task<List<MonitoringCheckEventRow>> GetFarmCheckEventsAsync(Guid farmId, CancellationToken ct = default);

    Task AddAsync(CropMonitoringCheck check, CancellationToken ct = default);
    void Update(CropMonitoringCheck check);
    Task SaveChangesAsync(CancellationToken ct = default);
}
