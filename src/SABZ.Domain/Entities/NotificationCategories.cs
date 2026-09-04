namespace SABZ.Domain.Entities;

/// <summary>
/// Farmer-facing notification categories (Prompt 8). Plain string constants so
/// new categories can be added by future modules without schema changes or
/// switch-based business rules. Prompt 8 itself only ever creates
/// <see cref="MonitoringDue"/> and <see cref="MonitoringPlan"/> (created by the
/// notification service); the remaining monitoring categories exist as documented
/// vocabulary for future, explicitly requested workflows.
/// </summary>
public static class NotificationCategories
{
    public const string MonitoringDue = "MonitoringDue";
    public const string MonitoringPlan = "MonitoringPlan";
    public const string MonitoringUpcoming = "MonitoringUpcoming";
    public const string MonitoringCompleted = "MonitoringCompleted";
    public const string MonitoringSkipped = "MonitoringSkipped";
    public const string System = "System";
}
