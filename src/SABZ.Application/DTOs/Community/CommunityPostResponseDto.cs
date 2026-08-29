namespace SABZ.Application.DTOs.Community;

/// <summary>
/// A community post as exposed to the API (Prompt 14). Never an EF entity and
/// never exposes the author's UserId, email or any other sensitive identity -
/// only the display name. IsOwnedByCurrentUser is computed server-side from
/// the JWT identity.
/// </summary>
public class CommunityPostResponseDto
{
    public Guid Id { get; set; }

    /// <summary>Author display name (User.FullName); never email or phone.</summary>
    public string AuthorName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Visible (non soft-deleted) comments, computed in SQL.</summary>
    public int CommentCount { get; set; }

    /// <summary>True when the JWT user authored this post (drives delete rights).</summary>
    public bool IsOwnedByCurrentUser { get; set; }
}
