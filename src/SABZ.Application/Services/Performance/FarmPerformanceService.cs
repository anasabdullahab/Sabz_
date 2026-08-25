using SABZ.Application.DTOs.Performance;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Performance;

/// <summary>
/// Farm Performance Dashboard &amp; Decision Intelligence (Prompt 11).
///
/// Design decisions:
/// - Pure read-only intelligence over existing SABZ data: crops, the Prompt 9
///   financial ledger and Prompt 7 monitoring checks. Every value is
///   aggregated at request time and nothing derived is ever persisted.
/// - Ownership follows the existing JWT user -> farm -> (crops, transactions,
///   checks) pattern; the client never supplies a UserId.
/// - All money arithmetic is decimal. All wording is factual and describes
///   only recorded data - never advice, scoring, credit, or predictions.
/// - The system never invents farm activity, financial rows, or rankings:
///   crops without recorded transactions are never ranked.
/// </summary>
public class FarmPerformanceService : IFarmPerformanceService
{
    // Overall performance status (deterministic, factual - NOT a score).
    public const string StatusNoRecordedData = "NoRecordedData";
    public const string StatusLimitedRecordedData = "LimitedRecordedData";
    public const string StatusRecordedActivityAvailable = "RecordedActivityAvailable";

    // Per-crop financial data status - recorded ledger sides only.
    public const string FinNoFinancialData = "NoFinancialData";
    public const string FinExpensesOnly = "ExpensesOnly";
    public const string FinIncomeOnly = "IncomeOnly";
    public const string FinRecordedIncomeAndExpenses = "RecordedIncomeAndExpenses";

    // Structured limitation codes.
    public const string LimitNoFinancialTransactions = "NoFinancialTransactions";
    public const string LimitCropsWithoutFinancialRecords = "CropsWithoutFinancialRecords";
    public const string LimitExpensesOnlyCrops = "ExpensesOnlyCrops";
    public const string LimitIncomeOnlyCrops = "IncomeOnlyCrops";
    public const string LimitUnattributedTransactions = "UnattributedTransactions";
    public const string LimitNoRankedCrops = "NoRankedCrops";

    // "Sufficient recorded activity" needs both ledger sides and at least
    // this many transactions (same threshold as Prompt 10 LimitedData).
    public const int SufficientTransactionThreshold = 5;

    public const string PerformanceDisclaimer =
        "Based only on data recorded in SABZ. This does not measure real-world farm performance, " +
        "farming skill, future outcomes, creditworthiness, or financial eligibility.";

    private readonly IFarmRepository _farmRepository;
    private readonly ICropRepository _cropRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly ICropMonitoringCheckRepository _checkRepository;

    public FarmPerformanceService(
        IFarmRepository farmRepository,
        ICropRepository cropRepository,
        IFinancialTransactionRepository transactionRepository,
        ICropMonitoringCheckRepository checkRepository)
    {
        _farmRepository = farmRepository;
        _cropRepository = cropRepository;
        _transactionRepository = transactionRepository;
        _checkRepository = checkRepository;
    }

    // ------------------------------------------------------------------
    //  Endpoint 1 - farm performance overview
    // ------------------------------------------------------------------

    public async Task<FarmPerformanceSummaryDto> GetPerformanceSummaryAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);
        var (from, to) = ValidateDateRange(fromDate, toDate);

        var crops = await _cropRepository.GetByFarmIdAsync(farm.Id);
        var stats = await _transactionRepository.GetHealthStatsAsync(farm.Id, cropId: null, from, to, ct);
        var cropTotals = await _transactionRepository.GetCropTotalsAsync(farm.Id, from, to, ct);
        var checks = await _checkRepository.GetFarmCheckEventsAsync(farm.Id, ct);

        var totalCount = stats.IncomeCount + stats.ExpenseCount;
        var netResult = stats.TotalIncome - stats.TotalExpenses;

        // Crop-linked totals joined with existing crop records; rows that no
        // longer match a crop of the farm can never be ranked.
        var ranked = cropTotals
            .Join(crops, t => t.CropId, c => c.Id, (t, c) => new RecordedCropPerformanceDto
            {
                CropId = c.Id,
                CropName = c.CropName,
                TotalIncome = t.IncomeTotal,
                TotalExpense = t.ExpenseTotal,
                NetResult = t.IncomeTotal - t.ExpenseTotal,
                IncomeTransactionCount = t.IncomeCount,
                ExpenseTransactionCount = t.ExpenseCount,
                TransactionCount = t.IncomeCount + t.ExpenseCount
            })
            .ToList();

        // Deterministic ranking: net result, then crop name (ordinal), then
        // crop id - documented in docs/prompt-11-farm-performance.md.
        var best = ranked
            .OrderByDescending(r => r.NetResult)
            .ThenBy(r => r.CropName, StringComparer.Ordinal)
            .ThenBy(r => r.CropId)
            .FirstOrDefault();

        var weakest = ranked
            .OrderBy(r => r.NetResult)
            .ThenBy(r => r.CropName, StringComparer.Ordinal)
            .ThenBy(r => r.CropId)
            .FirstOrDefault();

        var cropIdsWithActivity = ranked.Select(r => r.CropId).ToHashSet();
        var withActivity = crops.Count(c => cropIdsWithActivity.Contains(c.Id));

        var summary = new FarmPerformanceSummaryDto
        {
            FarmId = farm.Id,
            FarmName = farm.FarmName,
            FromDate = from,
            ToDate = to,
            TotalCrops = crops.Count,
            ActiveCrops = crops.Count(c => c.Status == "Active"),
            CropsWithFinancialActivity = withActivity,
            CropsWithoutFinancialActivity = crops.Count - withActivity,
            TransactionCount = totalCount,
            TotalIncome = stats.TotalIncome,
            TotalExpense = stats.TotalExpenses,
            NetResult = netResult,
            BestRecordedCrop = best,
            WeakestRecordedCrop = weakest,
            Limitations = BuildLimitations(crops, ranked, stats, from, to),
            Disclaimer = PerformanceDisclaimer
        };

        var completedOrSkipped = checks.Count(c => c.Status != MonitoringCheckStatus.Scheduled);
        (summary.OverallStatus, summary.StatusExplanation) =
            DetermineOverallStatus(totalCount, stats.IncomeCount, stats.ExpenseCount, completedOrSkipped);

        return summary;
    }

    // ------------------------------------------------------------------
    //  Endpoint 2 - per-crop performance breakdown
    // ------------------------------------------------------------------

    public async Task<List<CropPerformanceDto>> GetCropPerformanceAsync(
        Guid userId, Guid farmId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);
        var (from, to) = ValidateDateRange(fromDate, toDate);

        var crops = await _cropRepository.GetByFarmIdAsync(farm.Id);
        var cropTotals = (await _transactionRepository.GetCropTotalsAsync(farm.Id, from, to, ct))
            .ToDictionary(t => t.CropId);

        return crops
            .OrderBy(c => c.CropName, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .Select(c =>
            {
                cropTotals.TryGetValue(c.Id, out var totals);

                var income = totals?.IncomeTotal ?? 0m;
                var expense = totals?.ExpenseTotal ?? 0m;
                var incomeCount = totals?.IncomeCount ?? 0;
                var expenseCount = totals?.ExpenseCount ?? 0;

                return new CropPerformanceDto
                {
                    CropId = c.Id,
                    CropName = c.CropName,
                    Status = c.Status,
                    TransactionCount = incomeCount + expenseCount,
                    TotalIncome = income,
                    TotalExpense = expense,
                    NetResult = income - expense,
                    HasIncomeRecords = incomeCount > 0,
                    HasExpenseRecords = expenseCount > 0,
                    FinancialDataStatus = DetermineFinancialDataStatus(incomeCount, expenseCount)
                };
            })
            .ToList();
    }

    // ------------------------------------------------------------------
    //  Endpoint 3 - recorded activity in SABZ (full history, no range)
    // ------------------------------------------------------------------

    public async Task<FarmActivitySummaryDto> GetActivitySummaryAsync(Guid userId, Guid farmId, CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, ct);

        var transactionDates = await _transactionRepository.GetDistinctTransactionDatesAsync(farm.Id, ct);
        var ledgerStats = await _transactionRepository.GetHealthStatsAsync(farm.Id, cropId: null, fromDate: null, toDate: null, ct);
        var checks = await _checkRepository.GetFarmCheckEventsAsync(farm.Id, ct);

        var completed = checks.Count(c => c.Status == MonitoringCheckStatus.Completed);
        var skipped = checks.Count(c => c.Status == MonitoringCheckStatus.Skipped);
        var scheduled = checks.Count(c => c.Status == MonitoringCheckStatus.Scheduled);

        // Recorded activity events: financial transactions (TransactionDate)
        // plus completed/skipped checks at the moment they were acted on.
        // Scheduled checks are plans, not events.
        var eventDates = new HashSet<DateTime>(transactionDates.Select(d => d.Date));
        foreach (var check in checks)
        {
            if (check.Status == MonitoringCheckStatus.Completed && check.CompletedAt is not null)
                eventDates.Add(check.CompletedAt.Value.Date);
            if (check.Status == MonitoringCheckStatus.Skipped && check.SkippedAt is not null)
                eventDates.Add(check.SkippedAt.Value.Date);
        }

        return new FarmActivitySummaryDto
        {
            FarmId = farm.Id,
            FinancialTransactionCount = ledgerStats.IncomeCount + ledgerStats.ExpenseCount,
            MonitoringCheckCount = checks.Count,
            CompletedMonitoringChecks = completed,
            SkippedMonitoringChecks = skipped,
            ScheduledMonitoringChecks = scheduled,
            FirstRecordedActivity = eventDates.Count > 0 ? eventDates.Min().Date : null,
            LatestRecordedActivity = eventDates.Count > 0 ? eventDates.Max().Date : null,
            RecordedActivityDays = eventDates.Count,
            Explanation = "This summary reflects recorded activity in SABZ only: financial transactions entered by the farmer " +
                          "and completed or skipped monitoring checks. It does not represent physical farm activity."
        };
    }

    // ------------------------------------------------------------------
    //  Deterministic status helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Overall performance status, evaluated in order:
    /// - NoRecordedData: no financial transactions AND no completed/skipped checks.
    /// - RecordedActivityAvailable: at least <see cref="SufficientTransactionThreshold"/>
    ///   transactions with both income and expenses recorded.
    /// - LimitedRecordedData: everything in between.
    /// </summary>
    private static (string Status, string Explanation) DetermineOverallStatus(
        int transactionCount, int incomeCount, int expenseCount, int completedOrSkippedChecks)
    {
        if (transactionCount == 0 && completedOrSkippedChecks == 0)
            return (StatusNoRecordedData,
                "No financial transactions and no completed or skipped monitoring checks have been recorded for this farm.");

        if (transactionCount >= SufficientTransactionThreshold && incomeCount > 0 && expenseCount > 0)
            return (StatusRecordedActivityAvailable,
                $"At least {SufficientTransactionThreshold} financial transactions are recorded with both income and expenses, " +
                "so a meaningful recorded performance summary is available.");

        var gaps = new List<string>();
        if (transactionCount == 0)
            gaps.Add("no financial transactions are recorded");
        else if (transactionCount < SufficientTransactionThreshold)
            gaps.Add($"fewer than {SufficientTransactionThreshold} financial transactions are recorded");
        if (transactionCount > 0 && incomeCount == 0)
            gaps.Add("no income transactions are recorded");
        if (transactionCount > 0 && expenseCount == 0)
            gaps.Add("no expense transactions are recorded");

        return (StatusLimitedRecordedData,
            "Some recorded activity exists but the data is limited: " + string.Join("; ", gaps) + ".");
    }

    private static string DetermineFinancialDataStatus(int incomeCount, int expenseCount)
    {
        if (incomeCount == 0 && expenseCount == 0)
            return FinNoFinancialData;
        if (incomeCount > 0 && expenseCount > 0)
            return FinRecordedIncomeAndExpenses;
        return expenseCount > 0 ? FinExpensesOnly : FinIncomeOnly;
    }

    private static List<PerformanceLimitationDto> BuildLimitations(
        List<Crop> crops,
        List<RecordedCropPerformanceDto> ranked,
        FinancialHealthStats stats,
        DateTime? from,
        DateTime? to)
    {
        var period = from is not null || to is not null ? " in the selected period" : string.Empty;
        var totalCount = stats.IncomeCount + stats.ExpenseCount;
        var limitations = new List<PerformanceLimitationDto>();

        if (totalCount == 0)
            limitations.Add(new PerformanceLimitationDto
            {
                Code = LimitNoFinancialTransactions,
                Message = "No financial transactions have been recorded for this farm" + period + "."
            });

        var cropIdsWithActivity = ranked.Select(r => r.CropId).ToHashSet();
        var withoutRecords = crops.Count(c => !cropIdsWithActivity.Contains(c.Id));
        if (withoutRecords > 0)
            limitations.Add(new PerformanceLimitationDto
            {
                Code = LimitCropsWithoutFinancialRecords,
                Message = $"{withoutRecords} of {crops.Count} crops have no recorded financial transactions{period}."
            });

        var expensesOnly = ranked.Where(r => r.IncomeTransactionCount == 0 && r.ExpenseTransactionCount > 0)
            .Select(r => r.CropName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (expensesOnly.Count > 0)
            limitations.Add(new PerformanceLimitationDto
            {
                Code = LimitExpensesOnlyCrops,
                Message = "Recorded expenses but no recorded income" + period + " for: " + string.Join(", ", expensesOnly) + "."
            });

        var incomeOnly = ranked.Where(r => r.ExpenseTransactionCount == 0 && r.IncomeTransactionCount > 0)
            .Select(r => r.CropName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (incomeOnly.Count > 0)
            limitations.Add(new PerformanceLimitationDto
            {
                Code = LimitIncomeOnlyCrops,
                Message = "Recorded income but no recorded expenses" + period + " for: " + string.Join(", ", incomeOnly) + "."
            });

        if (stats.FarmLevelCount > 0)
            limitations.Add(new PerformanceLimitationDto
            {
                Code = LimitUnattributedTransactions,
                Message = $"{stats.FarmLevelCount} transactions{period} are not linked to a crop; they are included " +
                          "in the farm totals but excluded from the per-crop ranking."
            });

        if (ranked.Count == 0)
            limitations.Add(new PerformanceLimitationDto
            {
                Code = LimitNoRankedCrops,
                Message = "No crop has recorded financial transactions" + period +
                          ", so no crop is ranked as best or weakest recorded."
            });

        return limitations;
    }

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
