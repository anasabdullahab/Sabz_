namespace SABZ.Domain.Entities;

/// <summary>
/// A monitoring checkpoint scheduled for a specific crop (Prompt 7).
/// Ownership is derived through Crop -> Farm -> User; it is deliberately
/// not duplicated on this entity.
///
/// Only three states are persisted. "Upcoming" vs "Due" is computed by
/// comparing <see cref="ScheduledDate"/> with the current UTC time, so a
/// check is never automatically marked completed and a skipped check can
/// never reappear as due.
/// </summary>
public class CropMonitoringCheck
{
    public Guid Id { get; set; }
    public Guid CropId { get; set; }

    /// <summary>Rule that generated this check (null-safe: rules may be deactivated later).</summary>
    public int? RuleId { get; set; }

    /// <summary>Redundant denormalisation for cheap due/upcoming queries by farmer.</summary>
    public Guid FarmId { get; set; }

    public DateTime ScheduledDate { get; set; }

    /// <summary>Persisted lifecycle: Scheduled, Completed, Skipped.</summary>
    public MonitoringCheckStatus Status { get; set; } = MonitoringCheckStatus.Scheduled;

    // Snapshot of the rule at generation time so checks stay meaningful
    // even if the reference rule is edited or deactivated later.
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string InspectionItems { get; set; }
    public string Priority { get; set; } = "Medium";

    /// <summary>Farmer observation recorded on completion. Never a diagnosis.</summary>
    public MonitoringObservation? Observation { get; set; }

    public string? FarmerNotes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Crop Crop { get; set; } = null!;
    public CropMonitoringRule? Rule { get; set; }
}

/// <summary>Persisted lifecycle of a monitoring check.</summary>
public enum MonitoringCheckStatus
{
    Scheduled = 0,
    Completed = 1,
    Skipped = 2
}

/// <summary>
/// Controlled observation values a farmer may report when completing a check.
/// This records what the farmer saw - it is NOT a disease diagnosis.
/// </summary>
public enum MonitoringObservation
{
    Normal = 0,
    SomethingSuspicious = 1
}
