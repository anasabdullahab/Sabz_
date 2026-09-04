namespace SABZ.Domain.Entities;

/// <summary>
/// Central in-app notification (Prompt 8). Notifications live only inside the
/// SABZ database - no SMS/email/push provider is involved.
///
/// UserId is never accepted from clients; it is always derived server-side
/// from the owning entity chain (e.g. check -> crop -> farm -> user).
/// ReferenceType/ReferenceId are required so the duplicate-prevention unique
/// index has no nullable columns; notifications without a real reference use
/// <see cref="ReferenceTypes.None"/> and <see cref="Guid.Empty"/>.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public required string Title { get; set; }
    public required string Message { get; set; }

    /// <summary>Farmer-facing category, e.g. MonitoringDue (see NotificationCategories).</summary>
    public required string Category { get; set; }

    /// <summary>Kind of referenced entity, e.g. "CropMonitoringCheck".</summary>
    public string ReferenceType { get; set; } = ReferenceTypes.None;

    /// <summary>Id of the referenced entity; Guid.Empty when there is none.</summary>
    public Guid ReferenceId { get; set; } = Guid.Empty;

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public User? User { get; set; }
}

/// <summary>
/// Reference kinds a notification can point at. Extensible for future modules
/// (disease detection, weather alerts, ...) without schema changes.
/// </summary>
public static class ReferenceTypes
{
    public const string None = "None";
    public const string CropMonitoringCheck = "CropMonitoringCheck";
    public const string Crop = "Crop";
}
