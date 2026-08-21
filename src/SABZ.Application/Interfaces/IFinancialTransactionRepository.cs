using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Raw SQL-side aggregate rows for financial health intelligence (Prompt 10).
/// All values are computed from raw FinancialTransactions at request time;
/// nothing derived is ever persisted.
/// </summary>
public sealed record FinancialHealthStats(
    decimal TotalIncome,
    decimal TotalExpenses,
    int IncomeCount,
    int ExpenseCount,
    DateTime? FirstDate,
    DateTime? LastDate,
    int ActiveDays,
    int CropRelatedCount,
    int FarmLevelCount);

/// <summary>Per-category aggregate: one row per (TransactionType, Category).</summary>
public sealed record CategoryTotalRow(FinancialTransactionType Type, string Category, decimal Total, int Count);

/// <summary>Monthly aggregate with the calendar parts separated in SQL (yyyy-MM bucketing).</summary>
public sealed record MonthlyTotalRow(int Year, int Month, FinancialTransactionType Type, decimal Total, int Count);

/// <summary>
/// Financial ledger persistence (Prompt 9). All queries are farm-scoped;
/// user-scoping is enforced by the service via Farm.UserId.
/// </summary>
public interface IFinancialTransactionRepository
{
    /// <summary>Loads the transaction with Farm and Crop navigations for ownership checks.</summary>
    Task<FinancialTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Filtered farm transactions, newest TransactionDate first, capped at take.</summary>
    Task<List<FinancialTransaction>> GetByFarmIdAsync(
        Guid farmId,
        FinancialTransactionType? type,
        string? category,
        Guid? cropId,
        DateTime? fromDate,
        DateTime? toDate,
        int take,
        CancellationToken ct = default);

    /// <summary>Aggregated totals for P&L: (income sum, expense sum, entry count).</summary>
    Task<(decimal TotalIncome, decimal TotalExpenses, int Count)> GetTotalsAsync(
        Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    // ------------------------------------------------------------------
    //  Prompt 10 financial health aggregates (SQL-side, AsNoTracking,
    //  never persisted; extend the Prompt 9 GROUP BY aggregation pattern)
    // ------------------------------------------------------------------

    /// <summary>
    /// One-round-trip health statistics: totals and counts per transaction
    /// type, first/last transaction dates, distinct active days, and the
    /// crop-related vs farm-level split.
    /// </summary>
    Task<FinancialHealthStats> GetHealthStatsAsync(
        Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    /// <summary>Grouped totals per (TransactionType, Category) for breakdowns.</summary>
    Task<List<CategoryTotalRow>> GetCategoryTotalsAsync(
        Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    /// <summary>Grouped totals per (year, month, TransactionType) for monthly activity.</summary>
    Task<List<MonthlyTotalRow>> GetMonthlyTotalsAsync(
        Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);

    Task AddAsync(FinancialTransaction transaction, CancellationToken ct = default);
    void Update(FinancialTransaction transaction);
    void Remove(FinancialTransaction transaction);

    /// <summary>
    /// Stages CropId = null on every transaction of the crop (application-level
    /// SetNull; the database FK is Restrict). Shares the scoped DbContext, so
    /// the caller persists it together with the crop removal in one save.
    /// </summary>
    Task NullifyCropReferencesAsync(Guid cropId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
