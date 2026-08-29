namespace SABZ.Application.Interfaces;

/// <summary>
/// Community comment persistence (Prompt 14). All queries exclude
/// soft-deleted rows and project straight to read models.
/// </summary>
public interface ICommunityCommentRepository
{
    /// <summary>
    /// One DB-side page of a post's visible comments, oldest first
    /// (CreatedAt ASC, Id ASC), with the author display name computed in SQL.
    /// </summary>
    Task<(List<CommunityCommentReadModel> Items, int TotalCount)> GetPageAsync(
        Guid postId, Guid currentUserId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Bounded oldest-first first page for the post detail view.</summary>
    Task<List<CommunityCommentReadModel>> GetFirstPageAsync(
        Guid postId, Guid currentUserId, int take, CancellationToken ct = default);

    /// <summary>Tracked lookup for ownership checks and updates; null when missing or soft-deleted.</summary>
    Task<SABZ.Domain.Entities.CommunityComment?> FindTrackedByIdAsync(Guid commentId, CancellationToken ct = default);

    Task AddAsync(SABZ.Domain.Entities.CommunityComment comment, CancellationToken ct = default);

    void Update(SABZ.Domain.Entities.CommunityComment comment);

    /// <summary>
    /// Soft-deletes every visible comment of a post in one SQL statement
    /// (post deletion must not leave publicly visible comments behind).
    /// Returns how many comments were affected.
    /// </summary>
    Task<int> SoftDeleteActiveByPostAsync(Guid postId, DateTime deletedAt, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
