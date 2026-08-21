using Microsoft.Extensions.Logging;
using SABZ.Application.DTOs.Notifications;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Notifications;

/// <summary>
/// Central in-app notification service (Prompt 8). IN-APP ONLY: notifications
/// are rows in the SABZ database retrieved through the API - no SMS, email,
/// push or any external delivery channel exists or is claimed.
///
/// Design decisions:
/// - UserId is never accepted from clients. For monitoring notifications it is
///   derived from check -> crop -> farm -> user.
/// - Duplicate prevention is two-layered: an application-level existence
///   pre-check plus a database unique index on
///   (UserId, ReferenceType, ReferenceId, Category); the unique-index conflict
///   path is handled gracefully so concurrent requests never surface errors.
/// - Notification generation never changes monitoring state and never breaks
///   the monitoring read path (all failures are logged and swallowed).
/// </summary>
public class NotificationService : INotificationService
{
    private const int DefaultTake = 50;
    private const int MaxTake = 100;

    private readonly INotificationRepository _notificationRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        ISystemClock clock,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _clock = clock;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    //  Read operations (userId always comes from the JWT)
    // ------------------------------------------------------------------

    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, int? take, CancellationToken ct = default)
    {
        if (take is <= 0)
            throw new ValidationException("take must be a positive number.");

        var limit = Math.Min(take ?? DefaultTake, MaxTake);
        var notifications = await _notificationRepository.GetByUserIdAsync(userId, limit, ct);
        return notifications.Select(MapToDto).ToList();
    }

    public async Task<List<NotificationDto>> GetUnreadAsync(Guid userId, CancellationToken ct = default)
    {
        var notifications = await _notificationRepository.GetUnreadByUserIdAsync(userId, ct);
        return notifications.Select(MapToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => await _notificationRepository.CountUnreadAsync(userId, ct);

    public async Task<NotificationDto> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var notification = await GetOwnedNotificationAsync(userId, notificationId, ct);

        // Idempotent: marking an already-read notification succeeds and keeps
        // the original ReadAt timestamp (documented behaviour).
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = _clock.UtcNow;
            _notificationRepository.Update(notification);
            await _notificationRepository.SaveChangesAsync(ct);
        }

        return MapToDto(notification);
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _notificationRepository.GetUnreadByUserIdAsync(userId, ct);
        if (unread.Count == 0)
            return 0;

        var now = _clock.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
            _notificationRepository.Update(notification);
        }

        await _notificationRepository.SaveChangesAsync(ct);
        return unread.Count;
    }

    // ------------------------------------------------------------------
    //  Server-side generation (no client-facing creation endpoint exists)
    // ------------------------------------------------------------------

    public async Task<int> EnsureDueNotificationsAsync(IReadOnlyList<CropMonitoringCheck> dueChecks, CancellationToken ct = default)
    {
        if (dueChecks.Count == 0)
            return 0;

        // Application-level duplicate pre-check: one lookup per user covers all
        // of that user's due checks in this batch.
        var existingByUser = new Dictionary<Guid, HashSet<Guid>>();
        var created = 0;

        foreach (var check in dueChecks)
        {
            var userId = check.Crop?.Farm?.UserId;
            if (userId is null)
                continue; // ownership unknown -> never fabricate a recipient

            if (!existingByUser.TryGetValue(userId.Value, out var existingIds))
            {
                existingIds = await _notificationRepository.GetExistingReferenceIdsAsync(
                    userId.Value, ReferenceTypes.CropMonitoringCheck, NotificationCategories.MonitoringDue, ct);
                existingByUser[userId.Value] = existingIds;
            }

            if (existingIds.Contains(check.Id))
                continue; // at most one MonitoringDue notification per check

            await _notificationRepository.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Title = "Crop monitoring check due",
                Message = BuildDueMessage(check),
                Category = NotificationCategories.MonitoringDue,
                ReferenceType = ReferenceTypes.CropMonitoringCheck,
                ReferenceId = check.Id,
                IsRead = false,
                CreatedAt = _clock.UtcNow
            }, ct);

            // Database-level safety net: if a concurrent request inserted the
            // same notification first, the unique index rejects this row and
            // SaveChangesGuardedAsync detaches it instead of throwing.
            if (await _notificationRepository.SaveChangesGuardedAsync(ct))
            {
                existingIds.Add(check.Id);
                created++;
            }
        }

        return created;
    }

    private static string BuildDueMessage(CropMonitoringCheck check)
    {
        var cropName = check.Crop?.CropName ?? "your crop";
        var firstItems = string.Join(", ",
            check.InspectionItems
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(3));

        var message = $"Reminder: \"{check.Title}\" for {cropName} is due now. Inspect: {firstItems}.";
        return message.Length <= 1000 ? message : message[..997] + "...";
    }

    // ------------------------------------------------------------------
    //  Ownership (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<Notification> GetOwnedNotificationAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, ct)
            ?? throw new NotFoundException("Notification not found.");

        if (notification.UserId != userId)
            throw new ForbiddenException("You do not have access to this notification.");

        return notification;
    }

    private static NotificationDto MapToDto(Notification notification) => new()
    {
        Id = notification.Id,
        Title = notification.Title,
        Message = notification.Message,
        Category = notification.Category,
        ReferenceType = notification.ReferenceType,
        ReferenceId = notification.ReferenceId,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt
    };
}
