namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// Factual monitoring overview for the farm (Prompt 12 dashboard section).
/// Reuses Prompt 7 monitoring logic; the dashboard never changes monitoring
/// state and never adds a second notification mechanism.
/// </summary>
public class DashboardMonitoringSectionDto
{
    /// <summary>Scheduled checks whose date has passed and are not completed/skipped.</summary>
    public int DueChecks { get; set; }

    /// <summary>Scheduled checks whose date is in the future.</summary>
    public int UpcomingChecks { get; set; }

    public int CompletedChecks { get; set; }
    public int SkippedChecks { get; set; }

    /// <summary>Total monitoring checks recorded for the farm, any status.</summary>
    public int TotalChecks { get; set; }
}
