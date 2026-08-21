using SABZ.Application.DTOs.Financial;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Farm Financial Health & Readiness Intelligence (Prompt 10).
///
/// Read-only analytics computed dynamically from Prompt 9 FinancialTransactions.
/// This is NOT a loan, credit, banking, insurance, investment, or financing
/// system: it never approves anything, never invents transactions, persists
/// no derived values, and uses no AI. All operations are JWT-user-scoped
/// through farm (and crop) ownership; clients never supply a UserId.
/// </summary>
public interface IFinancialHealthService
{
    /// <summary>Deterministic health summary for a farm (optional date range).</summary>
    Task<FinancialHealthSummaryDto> GetFarmHealthAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    /// <summary>Income and expense category breakdowns with dynamic percentages.</summary>
    Task<CategoryBreakdownDto> GetCategoryBreakdownAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    /// <summary>Monthly (yyyy-MM) financial activity for a farm.</summary>
    Task<FinancialActivityDto> GetActivityAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    /// <summary>
    /// Financial record completeness (data readiness) over the farm's FULL
    /// history - five deterministic checks worth 20 points each. Not a credit
    /// score; no date-range parameters by design.
    /// </summary>
    Task<FinancialCompletenessDto> GetCompletenessAsync(
        Guid userId, Guid farmId, CancellationToken ct = default);

    /// <summary>Health summary scoped to one crop of the user's farm (optional date range).</summary>
    Task<FinancialHealthSummaryDto> GetCropHealthAsync(
        Guid userId, Guid farmId, Guid cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
}
