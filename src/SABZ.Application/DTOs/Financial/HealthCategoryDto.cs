namespace SABZ.Application.DTOs.Financial;

/// <summary>
/// One category's aggregated totals within a breakdown. Percentage is computed
/// dynamically against the relevant type total and never persisted.
/// </summary>
public class HealthCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }

    /// <summary>Share of the total income (or total expenses), 0-100, two decimals.</summary>
    public decimal Percentage { get; set; }
}
