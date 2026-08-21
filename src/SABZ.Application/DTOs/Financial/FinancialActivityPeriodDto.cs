namespace SABZ.Application.DTOs.Financial;

/// <summary>One monthly bucket (yyyy-MM) of financial activity. Derived dynamically.</summary>
public class FinancialActivityPeriodDto
{
    /// <summary>Calendar month in yyyy-MM format.</summary>
    public string Period { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal NetResult { get; set; }
    public int TransactionCount { get; set; }
}
