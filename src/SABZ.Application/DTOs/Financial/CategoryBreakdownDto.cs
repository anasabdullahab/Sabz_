namespace SABZ.Application.DTOs.Financial;

/// <summary>
/// Income and expense category breakdowns for a farm (optionally a crop and/or
/// date range). All values are computed dynamically from Prompt 9 transactions.
/// </summary>
public class CategoryBreakdownDto
{
    public Guid FarmId { get; set; }
    public Guid? CropId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public decimal TotalExpense { get; set; }
    public decimal TotalIncome { get; set; }

    public List<HealthCategoryDto> Expenses { get; set; } = [];
    public List<HealthCategoryDto> Income { get; set; } = [];
}
