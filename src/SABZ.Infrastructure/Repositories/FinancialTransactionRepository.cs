using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class FinancialTransactionRepository : IFinancialTransactionRepository
{
    private readonly SabzDbContext _context;

    public FinancialTransactionRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<FinancialTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.FinancialTransactions
            .Include(t => t.Farm)
            .Include(t => t.Crop)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<List<FinancialTransaction>> GetByFarmIdAsync(
        Guid farmId,
        FinancialTransactionType? type,
        string? category,
        Guid? cropId,
        DateTime? fromDate,
        DateTime? toDate,
        int take,
        CancellationToken ct = default)
    {
        var query = _context.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.FarmId == farmId);

        if (type is not null)
            query = query.Where(t => t.TransactionType == type);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        if (cropId is not null)
            query = query.Where(t => t.CropId == cropId);

        if (fromDate is not null)
            query = query.Where(t => t.TransactionDate >= fromDate);

        if (toDate is not null)
            query = query.Where(t => t.TransactionDate <= toDate);

        return await query
            .Include(t => t.Crop)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<(decimal TotalIncome, decimal TotalExpenses, int Count)> GetTotalsAsync(
        Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var query = _context.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.FarmId == farmId);

        if (cropId is not null)
            query = query.Where(t => t.CropId == cropId);

        if (fromDate is not null)
            query = query.Where(t => t.TransactionDate >= fromDate);

        if (toDate is not null)
            query = query.Where(t => t.TransactionDate <= toDate);

        // Single grouped aggregate: totals computed from raw rows, never stored.
        var grouped = await query
            .GroupBy(t => t.TransactionType)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var income = grouped.FirstOrDefault(g => g.Type == FinancialTransactionType.Income);
        var expense = grouped.FirstOrDefault(g => g.Type == FinancialTransactionType.Expense);

        return (
            income?.Total ?? 0m,
            expense?.Total ?? 0m,
            (income?.Count ?? 0) + (expense?.Count ?? 0));
    }

    // ------------------------------------------------------------------
    //  Prompt 10 financial health aggregates (SQL-side GROUP BY / SUM /
    //  COUNT / MIN / MAX, AsNoTracking; never persisted)
    // ------------------------------------------------------------------

    private IQueryable<FinancialTransaction> FilterForHealth(
        Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.FarmId == farmId);

        if (cropId is not null)
            query = query.Where(t => t.CropId == cropId);

        if (fromDate is not null)
            query = query.Where(t => t.TransactionDate >= fromDate);

        if (toDate is not null)
            query = query.Where(t => t.TransactionDate <= toDate);

        return query;
    }

    public async Task<FinancialHealthStats> GetHealthStatsAsync(
        Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var query = FilterForHealth(farmId, cropId, fromDate, toDate);

        // Totals and counts per transaction type (mirrors GetTotalsAsync).
        var byType = await query
            .GroupBy(t => t.TransactionType)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        // Date bounds, distinct active days and the crop-link split in one round trip.
        var span = await query
            .GroupBy(t => 1)
            .Select(g => new
            {
                First = g.Min(t => t.TransactionDate),
                Last = g.Max(t => t.TransactionDate),
                ActiveDays = g.Select(t => t.TransactionDate).Distinct().Count(),
                CropRelated = g.Count(t => t.CropId != null),
                FarmLevel = g.Count(t => t.CropId == null)
            })
            .FirstOrDefaultAsync(ct);

        var income = byType.FirstOrDefault(g => g.Type == FinancialTransactionType.Income);
        var expense = byType.FirstOrDefault(g => g.Type == FinancialTransactionType.Expense);

        return new FinancialHealthStats(
            TotalIncome: income?.Total ?? 0m,
            TotalExpenses: expense?.Total ?? 0m,
            IncomeCount: income?.Count ?? 0,
            ExpenseCount: expense?.Count ?? 0,
            FirstDate: span?.First,
            LastDate: span?.Last,
            ActiveDays: span?.ActiveDays ?? 0,
            CropRelatedCount: span?.CropRelated ?? 0,
            FarmLevelCount: span?.FarmLevel ?? 0);
    }

    public async Task<List<CategoryTotalRow>> GetCategoryTotalsAsync(
        Guid farmId, Guid? cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var query = FilterForHealth(farmId, cropId, fromDate, toDate);

        var grouped = await query
            .GroupBy(t => new { t.TransactionType, t.Category })
            .Select(g => new { g.Key.TransactionType, g.Key.Category, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        return grouped
            .Select(g => new CategoryTotalRow(g.TransactionType, g.Category, g.Total, g.Count))
            .ToList();
    }

    public async Task<List<MonthlyTotalRow>> GetMonthlyTotalsAsync(
        Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var query = FilterForHealth(farmId, cropId: null, fromDate, toDate);

        // Calendar parts are extracted in SQL; yyyy-MM formatting happens in
        // the service after the aggregation.
        var grouped = await query
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.TransactionType })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.TransactionType,
                Total = g.Sum(t => t.Amount),
                Count = g.Count()
            })
            .ToListAsync(ct);

        return grouped
            .Select(g => new MonthlyTotalRow(g.Year, g.Month, g.TransactionType, g.Total, g.Count))
            .ToList();
    }

    public async Task AddAsync(FinancialTransaction transaction, CancellationToken ct = default)
    {
        await _context.FinancialTransactions.AddAsync(transaction, ct);
    }

    public void Update(FinancialTransaction transaction)
    {
        _context.FinancialTransactions.Update(transaction);
    }

    public void Remove(FinancialTransaction transaction)
    {
        _context.FinancialTransactions.Remove(transaction);
    }

    public async Task NullifyCropReferencesAsync(Guid cropId, CancellationToken ct = default)
    {
        // Application-level SetNull: the database FK is Restrict (SQL Server
        // forbids the double cascade path Farms->Crops->FinancialTransactions).
        var referenced = await _context.FinancialTransactions
            .Where(t => t.CropId == cropId)
            .ToListAsync(ct);

        foreach (var transaction in referenced)
            transaction.CropId = null;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
