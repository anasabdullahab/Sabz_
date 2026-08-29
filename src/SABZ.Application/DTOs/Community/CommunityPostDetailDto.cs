namespace SABZ.Application.DTOs.Community;

/// <summary>Response of GET /api/community/posts/{postId}: post plus a bounded first page of comments.</summary>
public class CommunityPostDetailDto
{
    public CommunityPostResponseDto Post { get; set; } = new();

    /// <summary>
    /// Oldest-first, capped at the first page. Use
    /// GET /api/community/posts/{postId}/comments for the full thread.
    /// </summary>
    public List<CommunityCommentResponseDto> Comments { get; set; } = [];
}
