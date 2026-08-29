namespace SABZ.Application.Interfaces;

/// <summary>
/// SQL-projected community post row (Prompt 14): author display name and
/// visible comment count are computed in the query so the feed needs exactly
/// one round-trip per page (no N+1, no entities). UserId is never exposed.
/// </summary>
public sealed record CommunityPostReadModel(
    Guid Id,
    string AuthorName,
    string Content,
    string? ImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int CommentCount,
    bool IsOwnedByCurrentUser);
