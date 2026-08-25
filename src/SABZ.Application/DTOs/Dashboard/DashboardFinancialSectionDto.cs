namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// Concise financial snapshot of the farm's Prompt 9 ledger (Prompt 12
/// dashboard section). All values are the existing dynamic P&amp;L summary -
/// decimal money only, computed at request time, never persisted, never
/// invented. No loans, credit, banking, insurance, investments, payments,
/// budgets, or forecasts exist anywhere in this section.
/// </summary>
public class DashboardFinancialSectionDto
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }

    /// <summary>TotalIncome - TotalExpenses over all recorded transactions.</summary>
    public decimal NetResult { get; set; }

    public int TransactionCount { get; set; }
}
