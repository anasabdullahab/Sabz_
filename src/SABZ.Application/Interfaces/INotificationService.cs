using SABZ.Application.DTOs.Notifications;
using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Central in-app notification service (Prompt 8).
///
/// Read/mark-read operations serve the API (userId always comes from the JWT).
/// Creation happens exclusively through server-side workflows such as
/// <see cref="EnsureDueNotificationsAsync"/> - there is intentionally no API
/// endpoint that lets clients create arbitrary notifications or supply a UserId.
/// </summary>
public interface INotificationService
{
    Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, int? take, CancellationToken ct = default);
    Task<List<NotificationDto>> GetUnreadAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<NotificationDto> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Idempotent, concurrency-safe creation of MonitoringDue notifications for
    /// the given due checks. UserId is derived from check -> crop -> farm, never
    /// from client input. Returns how many notifications were actually created.
    /// </summary>
    Task<int> EnsureDueNotificationsAsync(IReadOnlyList<CropMonitoringCheck> dueChecks, CancellationToken ct = default);
}
