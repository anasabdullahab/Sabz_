using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Notifications;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

/// <summary>
/// Central in-app notifications (Prompt 8). IN-APP ONLY: notifications are
/// database rows retrieved through these endpoints - no SMS, email, push or
/// any external delivery channel exists or is claimed.
///
/// All endpoints require authentication and are strictly user-scoped: identity
/// comes from the JWT, never from the route or body, and users can only see or
/// mark their own notifications. There is intentionally no endpoint that lets
/// clients create notifications or supply a UserId; notifications are created
/// by server-side workflows only (currently: due-monitoring reminders).
/// </summary>
[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// The authenticated user's notifications, newest first, capped at
    /// <c>take</c> (default 50, maximum 100) to keep responses bounded.
    /// </summary>
    [HttpGet("api/notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int? take, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var notifications = await _notificationService.GetNotificationsAsync(userId, take, ct);
        return Ok(notifications);
    }

    /// <summary>The authenticated user's unread notifications, newest first.</summary>
    [HttpGet("api/notifications/unread")]
    public async Task<IActionResult> GetUnread(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var notifications = await _notificationService.GetUnreadAsync(userId, ct);
        return Ok(notifications);
    }

    /// <summary>The authenticated user's unread count, e.g. <c>{ "count": 5 }</c>.</summary>
    [HttpGet("api/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId, ct);
        return Ok(new UnreadCountResponseDto { Count = count });
    }

    /// <summary>
    /// Mark one of the authenticated user's notifications as read. Idempotent:
    /// marking an already-read notification succeeds and keeps the original
    /// readAt. Returns 404 for unknown ids and 403 for another user's
    /// notifications (IDOR protection).
    /// </summary>
    [HttpPatch("api/notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var notification = await _notificationService.MarkReadAsync(userId, notificationId, ct);
        return Ok(notification);
    }

    /// <summary>Mark all of the authenticated user's unread notifications as read.</summary>
    [HttpPatch("api/notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var markedRead = await _notificationService.MarkAllReadAsync(userId, ct);
        return Ok(new MarkAllReadResponseDto { MarkedRead = markedRead });
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new SABZ.Domain.Exceptions.AuthenticationException("Invalid token.");
        return userId;
    }
}
