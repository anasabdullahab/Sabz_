namespace SABZ.Domain.Entities;

/// <summary>
/// Farmer community post (Prompt 14). User-generated agricultural content
/// shared with all authenticated SABZ farmers.
///
/// UserId is never accepted from clients; it is always derived server-side
/// from the JWT identity. Posts are soft-deleted (<see cref="IsDeleted"/>) so
/// deleted content disappears from all normal community queries while keeping
/// referential integrity. Deleting a post also soft-deletes its comments so no
/// publicly visible comment outlives its post.
/// </summary>
public class CommunityPost
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public required string Content { get; set; }

    /// <summary>Optional safe image reference (HTTP/HTTPS URL only, never a filesystem path).</summary>
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;
    public ICollection<CommunityComment> Comments { get; set; } = new List<CommunityComment>();
}
