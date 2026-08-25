using SABZ.Application.DTOs.Performance;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Farm Performance Dashboard &amp; Decision Intelligence (Prompt 11).
/// Read-only intelligence computed dynamically from existing SABZ data
/// (crops, Prompt 9 ledger, Prompt 7 monitoring checks); nothing derived
/// is ever persisted. Ownership always comes from the JWT user.
/// </summary>
public interface IFarmPerformanceService
{
    /// <summary>
    /// Farm-level performance overview: crop counts, recorded financial
    /// totals, deterministic best/weakest recorded crop, overall status and
    /// structured limitations. Optional fromDate/toDate filter only the
    /// financial ledger rows (TransactionDate), never the crop records.
    /// </summary>
    Task<FarmPerformanceSummaryDto> GetPerformanceSummaryAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    /// <summary>
    /// Per-crop recorded performance breakdown of the farm, including the
    /// deterministic FinancialDataStatus of each crop. Optional date range
    /// semantics match the overview endpoint.
    /// </summary>
    Task<List<CropPerformanceDto>> GetCropPerformanceAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    /// <summary>
    /// Recorded activity in SABZ (never physical activity) over the farm's
    /// full recorded history - no date-range parameters by design.
    /// </summary>
    Task<FarmActivitySummaryDto> GetActivitySummaryAsync(Guid userId, Guid farmId, CancellationToken ct = default);
}
