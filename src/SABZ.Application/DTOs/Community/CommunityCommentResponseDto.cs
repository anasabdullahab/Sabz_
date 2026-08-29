namespace SABZ.Application.DTOs.Community;

/// <summary>
/// A community comment as exposed to the API (Prompt 14). Never an EF entity
/// and never exposes the author's UserId - only the display name.
/// IsOwnedByCurrentUser is computed server-side from the JWT identity.
/// </summary>
public class CommunityCommentResponseDto
{
    public Guid Id { get; set; }

    /// <summary>Author display name (User.FullName); never email or phone.</summary>
    public string AuthorName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>True when the JWT user authored this comment (drives delete rights).</summary>
    public bool IsOwnedByCurrentUser { get; set; }
}
