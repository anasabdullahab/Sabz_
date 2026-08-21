namespace SABZ.Application.DTOs.Financial;

/// <summary>
/// Dynamically computed profit & loss summary (never persisted).
/// NetProfitLoss = TotalIncome - TotalExpenses.
/// </summary>
public class FinancialSummaryResponseDto
{
    public Guid FarmId { get; set; }
    public Guid? CropId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfitLoss { get; set; }
    public int TransactionCount { get; set; }
}
