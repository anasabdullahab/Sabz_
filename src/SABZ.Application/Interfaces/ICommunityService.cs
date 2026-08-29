using SABZ.Application.DTOs.Community;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Farmer community service (Prompt 14): agricultural posts and comments for
/// all authenticated SABZ farmers. A discussion feature, not a social-network
/// clone - no likes, followers, messaging or AI moderation.
///
/// userId is never accepted from clients in any DTO; it always comes from the
/// JWT identity. Soft-deleted posts/comments disappear from every normal
/// query. No notifications and no AI calls belong to this feature.
/// </summary>
public interface ICommunityService
{
    /// <summary>DB-side paginated community feed, newest first.</summary>
    Task<PagedResult<CommunityPostResponseDto>> GetPostsAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<CommunityPostResponseDto> CreatePostAsync(
        Guid userId, CreateCommunityPostDto dto, CancellationToken ct = default);

    /// <summary>Post detail with a bounded oldest-first first page of comments.</summary>
    Task<CommunityPostDetailDto> GetPostAsync(Guid userId, Guid postId, CancellationToken ct = default);

    /// <summary>Owner-only soft delete; also hides all of the post's comments.</summary>
    Task DeletePostAsync(Guid userId, Guid postId, CancellationToken ct = default);

    /// <summary>DB-side paginated comments of a post, oldest first (page=1, pageSize=20, max 50).</summary>
    Task<PagedResult<CommunityCommentResponseDto>> GetCommentsAsync(
        Guid userId, Guid postId, int page, int pageSize, CancellationToken ct = default);

    Task<CommunityCommentResponseDto> CreateCommentAsync(
        Guid userId, Guid postId, CreateCommunityCommentDto dto, CancellationToken ct = default);

    /// <summary>Owner-only soft delete of a single comment.</summary>
    Task DeleteCommentAsync(Guid userId, Guid commentId, CancellationToken ct = default);
}
