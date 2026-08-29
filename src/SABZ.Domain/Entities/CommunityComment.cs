namespace SABZ.Domain.Entities;

/// <summary>
/// Comment on a community post (Prompt 14). User-generated discussion content
/// belonging to exactly one <see cref="CommunityPost"/>.
///
/// UserId is never accepted from clients; it is always derived server-side
/// from the JWT identity. Comments are soft-deleted (<see cref="IsDeleted"/>)
/// and are never publicly visible once their post is soft-deleted.
/// </summary>
public class CommunityComment
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }

    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public CommunityPost Post { get; set; } = null!;
    public User User { get; set; } = null!;
}
