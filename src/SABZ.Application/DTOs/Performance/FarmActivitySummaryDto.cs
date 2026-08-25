namespace SABZ.Application.DTOs.Performance;

/// <summary>
/// Summary of RECORDED activity in SABZ for one farm (Prompt 11) - never
/// physical farm activity. Computed at request time from existing data:
/// Prompt 9 financial transactions and Prompt 7 monitoring checks.
///
/// Recorded activity events are:
/// - financial transactions (by TransactionDate)
/// - completed monitoring checks (by CompletedAt)
/// - skipped monitoring checks (by SkippedAt)
///
/// Scheduled (not yet completed/skipped) checks are plans, not events; they
/// are counted separately and never contribute to activity dates. No date
/// range applies: the summary covers the farm's full recorded history.
/// </summary>
public class FarmActivitySummaryDto
{
    public Guid FarmId { get; set; }

    /// <summary>All recorded financial transactions of the farm.</summary>
    public int FinancialTransactionCount { get; set; }

    /// <summary>All monitoring checks of the farm, any status.</summary>
    public int MonitoringCheckCount { get; set; }
    public int CompletedMonitoringChecks { get; set; }
    public int SkippedMonitoringChecks { get; set; }
    public int ScheduledMonitoringChecks { get; set; }

    /// <summary>Earliest recorded activity event, null when nothing is recorded.</summary>
    public DateTime? FirstRecordedActivity { get; set; }

    /// <summary>Latest recorded activity event, null when nothing is recorded.</summary>
    public DateTime? LatestRecordedActivity { get; set; }

    /// <summary>Distinct calendar days with at least one recorded activity event.</summary>
    public int RecordedActivityDays { get; set; }

    /// <summary>Factual statement that this reflects recorded activity in SABZ only.</summary>
    public string Explanation { get; set; } = string.Empty;
}
