namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// Unified Farm Dashboard response (Prompt 12): a single read-only, farm-level
/// overview that combines EXISTING SABZ data and calculations - farm details,
/// crops, Prompt 7 monitoring, Prompt 8 notifications, the Prompt 9 ledger,
/// Prompt 10 financial health, Prompt 11 performance and (when available)
/// external Prompt 3 weather.
///
/// The dashboard is an aggregation/orchestration layer, never a new source of
/// truth: every value is retrieved or computed at request time from existing
/// services, nothing derived is persisted, and no UserId is ever accepted or
/// exposed.
/// </summary>
public class FarmDashboardDto
{
    public DashboardFarmSectionDto Farm { get; set; } = new();
    public DashboardCropsSectionDto Crops { get; set; } = new();
    public DashboardMonitoringSectionDto Monitoring { get; set; } = new();
    public DashboardNotificationsSectionDto Notifications { get; set; } = new();
    public DashboardFinancialSectionDto Financial { get; set; } = new();
    public DashboardFinancialHealthSectionDto FinancialHealth { get; set; } = new();
    public DashboardPerformanceSectionDto Performance { get; set; } = new();

    /// <summary>External current weather; null when coordinates or the provider are unavailable.</summary>
    public DashboardWeatherSectionDto? Weather { get; set; }

    /// <summary>Structured, factual limitations/data-context of this unified view.</summary>
    public List<DashboardLimitationDto> Limitations { get; set; } = [];

    /// <summary>Mandatory factual disclaimer about what the dashboard does and does not do.</summary>
    public string Disclaimer { get; set; } = string.Empty;

    /// <summary>When this unified view was assembled (UTC).</summary>
    public DateTime GeneratedAt { get; set; }
}
