namespace SABZ.Application.DTOs.Performance;

/// <summary>
/// Recorded financial result of one crop used for the deterministic
/// best/weakest ranking (Prompt 11). Only crops with at least one recorded
/// financial transaction are ever ranked; all values are pure aggregates of
/// Prompt 9 ledger rows, computed at request time and never persisted.
/// </summary>
public class RecordedCropPerformanceDto
{
    public Guid CropId { get; set; }
    public string CropName { get; set; } = string.Empty;

    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetResult { get; set; }

    public int IncomeTransactionCount { get; set; }
    public int ExpenseTransactionCount { get; set; }
    public int TransactionCount { get; set; }
}
