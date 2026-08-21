namespace SABZ.Application.DTOs.Notifications;

/// <summary>
/// A notification as exposed to the API (Prompt 8). Never an EF entity and
/// never exposes the owning UserId - identity always comes from the JWT.
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>Response of GET /api/notifications/unread-count.</summary>
public class UnreadCountResponseDto
{
    public int Count { get; set; }
}

/// <summary>Response of PATCH /api/notifications/read-all.</summary>
public class MarkAllReadResponseDto
{
    /// <summary>How many notifications were actually marked read by this call.</summary>
    public int MarkedRead { get; set; }
}
