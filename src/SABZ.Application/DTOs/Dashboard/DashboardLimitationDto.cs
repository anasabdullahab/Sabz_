namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// A structured, factual limitation/data-context note of the unified farm
/// dashboard (Prompt 12). Each limitation has a stable machine-readable Code
/// plus a human message - never hidden in free text.
/// </summary>
public class DashboardLimitationDto
{
    /// <summary>Stable limitation code (e.g. NoFinancialTransactions).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Factual description based only on recorded/calculated SABZ data.</summary>
    public string Message { get; set; } = string.Empty;
}
