using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

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
