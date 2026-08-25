using SABZ.Application.DTOs.Notifications;

namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// Notification overview (Prompt 12 dashboard section). Reuses Prompt 8;
/// the dashboard only READS notification data and never creates
/// notifications. All data is user-scoped through the JWT; UserId is never
/// exposed.
/// </summary>
public class DashboardNotificationsSectionDto
{
    /// <summary>The user's unread notifications (user-scoped, not farm-scoped).</summary>
    public int UnreadCount { get; set; }

    /// <summary>Bounded list of the user's most recent notifications, newest first.</summary>
    public List<NotificationDto> RecentNotifications { get; set; } = [];
}
