using SABZ.Application.DTOs.Dashboard;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Unified Farm Dashboard &amp; Insights (Prompt 12). A read-only
/// aggregation/orchestration layer over EXISTING SABZ features (farms,
/// crops, Prompt 7 monitoring, Prompt 8 notifications, Prompt 9 ledger,
/// Prompt 10 financial health, Prompt 11 performance, Prompt 3 weather).
/// Nothing derived is ever persisted; no new tables, no migrations, no
/// caching, no background jobs, no AI. Ownership always comes from the JWT
/// user; clients never supply a UserId.
/// </summary>
public interface IFarmDashboardService
{
    /// <summary>
    /// The unified dashboard for one farm of the authenticated user:
    /// 404 when the farm does not exist, 403 when it belongs to another user.
    /// </summary>
    Task<FarmDashboardDto> GetDashboardAsync(Guid userId, Guid farmId, CancellationToken ct = default);
}
