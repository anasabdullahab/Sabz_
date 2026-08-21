namespace SABZ.Application.DTOs.Financial;

/// <summary>
/// Monthly financial activity for a farm over the selected range. Periods are
/// grouped in SQL (yyyy-MM) and ordered oldest first; nothing is persisted.
/// </summary>
public class FinancialActivityDto
{
    public Guid FarmId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>Total income across all periods in the range.</summary>
    public decimal TotalIncome { get; set; }

    /// <summary>Total expenses across all periods in the range.</summary>
    public decimal TotalExpense { get; set; }

    /// <summary>TotalIncome - TotalExpense across all periods in the range.</summary>
    public decimal NetResult { get; set; }

    /// <summary>Total transaction count across all periods in the range.</summary>
    public int TotalTransactionCount { get; set; }

    public List<FinancialActivityPeriodDto> Periods { get; set; } = [];
}
