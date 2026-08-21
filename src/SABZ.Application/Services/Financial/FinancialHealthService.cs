using SABZ.Application.DTOs.Financial;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Financial;

/// <summary>
/// Farm Financial Health & Readiness Intelligence (Prompt 10).
///
/// Design decisions:
/// - Pure read-only analytics over the Prompt 9 ledger; every value is
///   aggregated in SQL at request time and nothing derived is persisted.
/// - Ownership follows the existing JWT user -> farm (-> crop) pattern;
///   the client never supplies a UserId.
/// - All money arithmetic is decimal. All wording is factual and describes
///   only recorded data - never advice, credit scoring, or approvals.
/// - The system never invents financial transactions.
/// </summary>
public class FinancialHealthService : IFinancialHealthService
{
    // Health indicator states (deterministic, factual).
    public const string IndicatorNoData = "NoData";
    public const string IndicatorLimitedData = "LimitedData";
    public const string IndicatorLossRecorded = "LossRecorded";
    public const string IndicatorBreakEven = "BreakEven";
    public const string IndicatorPositiveNetResult = "PositiveNetResult";

    // Completeness statuses - recorded DATA completeness only.
    public const string StatusNoData = "NoData";
    public const string StatusPartial = "Partial";
    public const string StatusComplete = "Complete";

    public const int LimitedDataTransactionThreshold = 5;
    public const int CompletenessMinimumTransactions = 10;
    public const int CompletenessMinimumHistoryDays = 30;
    public const int CompletenessMinimumActiveDays = 3;
    public const int CompletenessPointsPerCheck = 20;

    public const string CompletenessDisclaimer = "Based only on transactions entered into SABZ.";

    private readonly IFarmRepository _farmRepository;
    private readonly ICropRepository _cropRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;

    public FinancialHealthService(
        IFarmRepository farmRepository,
        ICropRepository cropRepository,
        IFinancialTransactionRepository transactionRepository)
    {
        _farmRepository = farmRepository;
        _cropRepository = cropRepository;
        _transactionRepository = transactionRepository;
    }

    // ------------------------------------------------------------------
    //  Endpoint 1 - farm financial health summary
    // ------------------------------------------------------------------

    public async Task<FinancialHealthSummaryDto> GetFarmHealthAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);
        var (from, to) = ValidateDateRange(fromDate, toDate);

        var stats = await _transactionRepository.GetHealthStatsAsync(farm.Id, cropId: null, from, to, ct);
        return BuildSummary(farm.Id, cropId: null, from, to, stats, scope: "farm");
    }

    // ------------------------------------------------------------------
    //  Endpoint 2 - category breakdown
    // ------------------------------------------------------------------

    public async Task<CategoryBreakdownDto> GetCategoryBreakdownAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);
        var (from, to) = ValidateDateRange(fromDate, toDate);

        var rows = await _transactionRepository.GetCategoryTotalsAsync(farm.Id, cropId: null, from, to, ct);

        var expenses = rows.Where(r => r.Type == FinancialTransactionType.Expense).ToList();
        var income = rows.Where(r => r.Type == FinancialTransactionType.Income).ToList();
        var totalExpense = expenses.Sum(r => r.Total);
        var totalIncome = income.Sum(r => r.Total);

        return new CategoryBreakdownDto
        {
            FarmId = farm.Id,
            FromDate = from,
            ToDate = to,
            TotalExpense = totalExpense,
            TotalIncome = totalIncome,
            Expenses = BuildCategories(expenses, totalExpense),
            Income = BuildCategories(income, totalIncome)
        };
    }

    // ------------------------------------------------------------------
    //  Endpoint 3 - monthly financial activity
    // ------------------------------------------------------------------

    public async Task<FinancialActivityDto> GetActivityAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);
        var (from, to) = ValidateDateRange(fromDate, toDate);

        var rows = await _transactionRepository.GetMonthlyTotalsAsync(farm.Id, from, to, ct);

        var periods = rows
            .GroupBy(r => (r.Year, r.Month))
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var income = g.Where(r => r.Type == FinancialTransactionType.Income).Sum(r => r.Total);
                var expense = g.Where(r => r.Type == FinancialTransactionType.Expense).Sum(r => r.Total);

                return new FinancialActivityPeriodDto
                {
                    Period = $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                    Income = income,
                    Expense = expense,
                    NetResult = income - expense,
                    TransactionCount = g.Sum(r => r.Count)
                };
            })
            .ToList();

        var totalIncome = periods.Sum(p => p.Income);
        var totalExpense = periods.Sum(p => p.Expense);

        return new FinancialActivityDto
        {
            FarmId = farm.Id,
            FromDate = from,
            ToDate = to,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            NetResult = totalIncome - totalExpense,
            TotalTransactionCount = periods.Sum(p => p.TransactionCount),
            Periods = periods
        };
    }

    // ------------------------------------------------------------------
    //  Endpoint 4 - financial record completeness (full history, no range)
    // ------------------------------------------------------------------

    public async Task<FinancialCompletenessDto> GetCompletenessAsync(
        Guid userId, Guid farmId, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);

        var stats = await _transactionRepository.GetHealthStatsAsync(farm.Id, cropId: null, fromDate: null, toDate: null, ct);

        var totalCount = stats.IncomeCount + stats.ExpenseCount;
        var historyDays = stats.FirstDate is not null && stats.LastDate is not null
            ? (stats.LastDate.Value - stats.FirstDate.Value).Days
            : 0;

        var checks = new List<FinancialCompletenessCheckDto>
        {
            new()
            {
                Name = "TransactionsExist",
                Passed = totalCount >= 1,
                Description = "At least one financial transaction has been recorded."
            },
            new()
            {
                Name = "MinimumTransactionCount",
                Passed = totalCount >= CompletenessMinimumTransactions,
                Description = $"At least {CompletenessMinimumTransactions} financial transactions have been recorded."
            },
            new()
            {
                Name = "BothTypesRepresented",
                Passed = stats.IncomeCount > 0 && stats.ExpenseCount > 0,
                Description = "Both income and expense transactions have been recorded."
            },
            new()
            {
                Name = "HistorySpan",
                Passed = historyDays >= CompletenessMinimumHistoryDays,
                Description = $"Recorded history spans at least {CompletenessMinimumHistoryDays} days."
            },
            new()
            {
                Name = "ActiveDays",
                Passed = stats.ActiveDays >= CompletenessMinimumActiveDays,
                Description = $"At least {CompletenessMinimumActiveDays} distinct transaction dates have been recorded."
            }
        };

        var score = checks.Count(c => c.Passed) * CompletenessPointsPerCheck;

        string status;
        string explanation;
        if (totalCount == 0)
        {
            status = StatusNoData;
            explanation = "No financial transactions have been recorded for this farm. " +
                          "Financial summaries cannot be generated yet.";
        }
        else if (score == 100)
        {
            status = StatusComplete;
            explanation = "All 5 recorded-data completeness checks passed (score 100/100). " +
                          "The financial history entered into SABZ is sufficient for SABZ to generate summaries. " +
                          CompletenessDisclaimer;
        }
        else
        {
            status = StatusPartial;
            explanation = $"Recorded financial data is partial: {checks.Count(c => c.Passed)} of 5 " +
                          $"completeness checks passed (score {score}/100). " + CompletenessDisclaimer;
        }

        return new FinancialCompletenessDto
        {
            FarmId = farm.Id,
            Status = status,
            Score = score,
            Explanation = explanation,
            Limitations =
            [
                "This measures recorded financial data completeness only.",
                "It is not a credit score, loan eligibility, insurance eligibility, or any financial approval.",
                "Missing records do not mean the farm has no income or expenses."
            ],
            Disclaimer = CompletenessDisclaimer,
            Checks = checks
        };
    }

    // ------------------------------------------------------------------
    //  Endpoint 5 - crop financial health
    // ------------------------------------------------------------------

    public async Task<FinancialHealthSummaryDto> GetCropHealthAsync(
        Guid userId, Guid farmId, Guid cropId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);
        var (from, to) = ValidateDateRange(fromDate, toDate);
        var crop = await ValidateCropAsync(farm.Id, cropId, ct);

        var stats = await _transactionRepository.GetHealthStatsAsync(farm.Id, crop.Id, from, to, ct);
        return BuildSummary(farm.Id, crop.Id, from, to, stats, scope: "crop");
    }

    // ------------------------------------------------------------------
    //  Indicator / mapping helpers
    // ------------------------------------------------------------------

    private static FinancialHealthSummaryDto BuildSummary(
        Guid farmId, Guid? cropId, DateTime? from, DateTime? to, FinancialHealthStats stats, string scope)
    {
        var totalCount = stats.IncomeCount + stats.ExpenseCount;
        var netResult = stats.TotalIncome - stats.TotalExpenses;
        var scopeText = scope == "crop" ? "the selected crop" : "the selected farm";

        var (indicator, explanation) = DetermineIndicator(stats, totalCount, netResult, scopeText);

        return new FinancialHealthSummaryDto
        {
            FarmId = farmId,
            CropId = cropId,
            FromDate = from,
            ToDate = to,
            TotalIncome = stats.TotalIncome,
            TotalExpense = stats.TotalExpenses,
            NetResult = netResult,
            IncomeTransactionCount = stats.IncomeCount,
            ExpenseTransactionCount = stats.ExpenseCount,
            TotalTransactionCount = totalCount,
            FirstTransactionDate = stats.FirstDate,
            LastTransactionDate = stats.LastDate,
            NumberOfActiveFinancialDays = stats.ActiveDays,
            CropRelatedTransactionCount = stats.CropRelatedCount,
            FarmLevelTransactionCount = stats.FarmLevelCount,
            HealthIndicator = indicator,
            HealthExplanation = explanation
        };
    }

    private static (string Indicator, string Explanation) DetermineIndicator(
        FinancialHealthStats stats, int totalCount, decimal netResult, string scopeText)
    {
        if (totalCount == 0)
            return (IndicatorNoData,
                $"No financial transactions have been recorded for {scopeText} in the selected period.");

        var limitations = new List<string>();
        if (totalCount < LimitedDataTransactionThreshold)
            limitations.Add($"fewer than {LimitedDataTransactionThreshold} transactions recorded");
        if (stats.IncomeCount == 0)
            limitations.Add("no income transactions recorded");
        if (stats.ExpenseCount == 0)
            limitations.Add("no expense transactions recorded");

        if (limitations.Count > 0)
            return (IndicatorLimitedData,
                $"Recorded data is limited for {scopeText}: {string.Join("; ", limitations)}.");

        if (netResult < 0)
            return (IndicatorLossRecorded,
                $"Recorded expenses exceed recorded income for {scopeText} in the selected period.");

        if (netResult == 0)
            return (IndicatorBreakEven,
                $"Recorded income equals recorded expenses for {scopeText} in the selected period.");

        return (IndicatorPositiveNetResult,
            $"Recorded income exceeds recorded expenses for {scopeText} in the selected period.");
    }

    private static List<HealthCategoryDto> BuildCategories(List<CategoryTotalRow> rows, decimal total) => rows
        .OrderByDescending(r => r.Total)
        .ThenBy(r => r.Category, StringComparer.Ordinal)
        .Select(r => new HealthCategoryDto
        {
            Category = r.Category,
            Amount = r.Total,
            TransactionCount = r.Count,
            // Dynamic share of the relevant type total, never persisted.
            Percentage = total > 0 ? Math.Round(r.Total * 100m / total, 2) : 0m
        })
        .ToList();

    // ------------------------------------------------------------------
    //  Ownership (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId, CancellationToken ct)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return farm;
    }

    private async Task<Crop> ValidateCropAsync(Guid farmId, Guid cropId, CancellationToken ct)
    {
        var crop = await _cropRepository.GetByIdAsync(cropId)
            ?? throw new NotFoundException("Crop not found.");

        if (crop.FarmId != farmId)
            throw new ValidationException("Selected crop does not belong to the selected farm.");

        return crop;
    }

    private static (DateTime? From, DateTime? To) ValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        DateTime? from = fromDate?.Date;
        DateTime? to = toDate?.Date;

        if (from is not null && to is not null && from > to)
            throw new ValidationException("fromDate must be on or before toDate.");

        // Normalise to UTC midnight so range filters compare like stored dates.
        from = from is null ? null : new DateTime(from.Value.Year, from.Value.Month, from.Value.Day, 0, 0, 0, DateTimeKind.Utc);
        to = to is null ? null : new DateTime(to.Value.Year, to.Value.Month, to.Value.Day, 0, 0, 0, DateTimeKind.Utc);

        return (from, to);
    }
}
