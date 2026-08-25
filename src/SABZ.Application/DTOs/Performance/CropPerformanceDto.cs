namespace SABZ.Application.DTOs.Performance;

/// <summary>
/// Recorded financial performance of one crop of the farm (Prompt 11).
/// Derived at request time from the Prompt 9 ledger; nothing is persisted.
/// FinancialDataStatus describes which sides of the ledger have recorded
/// rows - the system never invents the missing side.
/// </summary>
public class CropPerformanceDto
{
    public Guid CropId { get; set; }
    public string CropName { get; set; } = string.Empty;

    /// <summary>Existing SABZ crop status (Active, Harvested, Failed, Planned).</summary>
    public string Status { get; set; } = string.Empty;

    public int TransactionCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetResult { get; set; }

    public bool HasIncomeRecords { get; set; }
    public bool HasExpenseRecords { get; set; }

    /// <summary>Deterministic state: NoFinancialData, ExpensesOnly, IncomeOnly, RecordedIncomeAndExpenses.</summary>
    public string FinancialDataStatus { get; set; } = string.Empty;
}
