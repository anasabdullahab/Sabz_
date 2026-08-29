namespace SABZ.Application.Interfaces;

/// <summary>
/// Community post persistence (Prompt 14). All queries exclude soft-deleted
/// rows and project straight to read models (no entity loading on read paths).
/// </summary>
public interface ICommunityPostRepository
{
    /// <summary>
    /// One DB-side page of the community feed, newest first
    /// (CreatedAt DESC, Id DESC), with author name and visible comment count
    /// computed in SQL.
    /// </summary>
    Task<(List<CommunityPostReadModel> Items, int TotalCount)> GetPageAsync(
        int page, int pageSize, Guid currentUserId, CancellationToken ct = default);

    /// <summary>Single post by id (404 semantics when missing or soft-deleted).</summary>
    Task<CommunityPostReadModel?> GetByIdAsync(Guid postId, Guid currentUserId, CancellationToken ct = default);

    /// <summary>Tracked lookup for ownership checks and updates; null when missing or soft-deleted.</summary>
    Task<SABZ.Domain.Entities.CommunityPost?> FindTrackedByIdAsync(Guid postId, CancellationToken ct = default);

    /// <summary>Author display name (User.FullName) or null; never email/phone.</summary>
    Task<string?> GetAuthorNameAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(SABZ.Domain.Entities.CommunityPost post, CancellationToken ct = default);

    void Update(SABZ.Domain.Entities.CommunityPost post);

    Task SaveChangesAsync(CancellationToken ct = default);
}
