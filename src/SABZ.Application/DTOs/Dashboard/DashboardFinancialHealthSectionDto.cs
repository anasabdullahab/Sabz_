namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// Financial health snapshot reusing Prompt 10 (Prompt 12 dashboard section).
/// Existing indicator/explanation and completeness only - NO new scoring and
/// nothing that could read as creditworthiness, loan eligibility, financing
/// eligibility, or investment rating.
/// </summary>
public class DashboardFinancialHealthSectionDto
{
    /// <summary>Existing Prompt 10 indicator: NoData, LimitedData, LossRecorded, BreakEven, PositiveNetResult.</summary>
    public string HealthIndicator { get; set; } = string.Empty;

    /// <summary>Existing factual Prompt 10 explanation - never advice.</summary>
    public string HealthExplanation { get; set; } = string.Empty;

    /// <summary>Existing Prompt 10 completeness status: NoData, Partial, Complete.</summary>
    public string CompletenessStatus { get; set; } = string.Empty;

    /// <summary>Existing Prompt 10 record-completeness score (0-100); NOT a credit score.</summary>
    public int CompletenessScore { get; set; }

    /// <summary>Existing Prompt 10 disclaimer, preserved unchanged.</summary>
    public string Disclaimer { get; set; } = string.Empty;
}
