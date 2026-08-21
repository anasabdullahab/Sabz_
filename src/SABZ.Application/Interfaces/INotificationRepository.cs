using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Notification persistence (Prompt 8). All reads are user-scoped server-side;
/// another user's notification is never returned.
/// </summary>
public interface INotificationRepository
{
    /// <summary>The user's notifications, newest first, capped at <paramref name="take"/>.</summary>
    Task<List<Notification>> GetByUserIdAsync(Guid userId, int take, CancellationToken ct = default);

    /// <summary>The user's unread notifications, newest first.</summary>
    Task<List<Notification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Ids of the user's existing notifications for the given reference + category (duplicate pre-check).</summary>
    Task<HashSet<Guid>> GetExistingReferenceIdsAsync(Guid userId, string referenceType, string category, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Plain save (updates such as marking read); errors propagate normally.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves changes; if the duplicate-prevention unique index rejects the row
    /// (concurrent generation race), the entry is detached, false is returned and
    /// the caller continues gracefully.
    /// </summary>
    Task<bool> SaveChangesGuardedAsync(CancellationToken ct = default);

    void Update(Notification notification);
}
