namespace SABZ.Application.DTOs.Performance;

/// <summary>
/// Dynamically computed farm performance overview (Prompt 11). Every value is
/// derived at request time from existing SABZ data (crops, Prompt 9 ledger,
/// Prompt 7 monitoring checks); nothing derived is persisted and no
/// UserId/ownership details are exposed. Money is decimal only.
///
/// "Best recorded" / "weakest recorded" describe only recorded financial
/// rows in the selected period - never real-world profitability.
/// </summary>
public class FarmPerformanceSummaryDto
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    // Farm overview (crop records; unaffected by the optional date range).
    public int TotalCrops { get; set; }
    public int ActiveCrops { get; set; }
    public int CropsWithFinancialActivity { get; set; }
    public int CropsWithoutFinancialActivity { get; set; }

    // Recorded financial overview (Prompt 9 ledger aggregates).
    public int TransactionCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetResult { get; set; }

    /// <summary>Crop with the best recorded net result, null when no crop has recorded transactions.</summary>
    public RecordedCropPerformanceDto? BestRecordedCrop { get; set; }

    /// <summary>Crop with the weakest recorded net result, null when no crop has recorded transactions.</summary>
    public RecordedCropPerformanceDto? WeakestRecordedCrop { get; set; }

    /// <summary>Deterministic state: NoRecordedData, LimitedRecordedData, RecordedActivityAvailable.</summary>
    public string OverallStatus { get; set; } = string.Empty;

    /// <summary>Factual description of how the status was determined from recorded data.</summary>
    public string StatusExplanation { get; set; } = string.Empty;

    /// <summary>Structured, honest limitations of the recorded data.</summary>
    public List<PerformanceLimitationDto> Limitations { get; set; } = [];

    /// <summary>Mandatory factual disclaimer about the data source.</summary>
    public string Disclaimer { get; set; } = string.Empty;
}
