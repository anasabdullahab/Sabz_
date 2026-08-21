namespace SABZ.Application.DTOs.Financial;

/// <summary>
/// Dynamically computed financial health summary (Prompt 10). Every value is
/// derived at request time from Prompt 9 FinancialTransactions; nothing is
/// persisted and no UserId/ownership details are exposed. NetResult is always
/// TotalIncome - TotalExpense (decimal only).
/// Used for both farm-level and crop-level financial health.
/// </summary>
public class FinancialHealthSummaryDto
{
    public Guid FarmId { get; set; }
    public Guid? CropId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetResult { get; set; }

    public int IncomeTransactionCount { get; set; }
    public int ExpenseTransactionCount { get; set; }
    public int TotalTransactionCount { get; set; }

    public DateTime? FirstTransactionDate { get; set; }
    public DateTime? LastTransactionDate { get; set; }
    public int NumberOfActiveFinancialDays { get; set; }

    /// <summary>Farm-scoped transactions with a linked crop (crop endpoint: equals TotalTransactionCount).</summary>
    public int CropRelatedTransactionCount { get; set; }

    /// <summary>Farm-scoped transactions without a linked crop (crop endpoint: always 0).</summary>
    public int FarmLevelTransactionCount { get; set; }

    /// <summary>Deterministic state: NoData, LimitedData, LossRecorded, BreakEven, PositiveNetResult.</summary>
    public string HealthIndicator { get; set; } = string.Empty;

    /// <summary>Factual description of the recorded data only - never financial advice.</summary>
    public string HealthExplanation { get; set; } = string.Empty;
}
