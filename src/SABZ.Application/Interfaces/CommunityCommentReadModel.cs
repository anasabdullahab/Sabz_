namespace SABZ.Application.Interfaces;

/// <summary>
/// SQL-projected community comment row (Prompt 14): author display name is
/// computed in the query (no N+1, no entities). UserId is never exposed.
/// </summary>
public sealed record CommunityCommentReadModel(
    Guid Id,
    string AuthorName,
    string Content,
    DateTime CreatedAt,
    bool IsOwnedByCurrentUser);
