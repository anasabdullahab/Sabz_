using SABZ.Application.DTOs.Community;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Community;

/// <summary>
/// Farmer community service (Prompt 14). Agriculture-focused discussion:
/// farmers post experiences and comment on each other's posts. Deliberately
/// NOT a social-media clone: no likes, followers, messaging, groups or AI
/// moderation, and this feature never creates notifications.
///
/// Design decisions:
/// - Identity always comes from the JWT (userId parameter is server-derived);
///   no DTO accepts a client-supplied user id.
/// - Soft-delete everywhere: deleted posts/comments vanish from all normal
///   queries while keeping referential integrity. Deleting a post also
///   soft-deletes its comments so nothing visible outlives its post.
/// - Read paths are DB-side paginated SQL projections (author name and
///   comment counts computed in SQL) - one round-trip per page, no N+1.
/// - Image support is a safe URL reference only (absolute HTTP/HTTPS).
///   Nothing is uploaded, stored as binary, or written to the filesystem,
///   and no cloud storage is invented.
/// </summary>
public class CommunityService : ICommunityService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;
    private const int MaxPostContentLength = 2000;
    private const int MaxCommentContentLength = 1000;
    private const int MaxImageUrlLength = 2048;
    private const int DetailCommentsPerPage = 50;

    private readonly ICommunityPostRepository _postRepository;
    private readonly ICommunityCommentRepository _commentRepository;
    private readonly ISystemClock _clock;

    public CommunityService(
        ICommunityPostRepository postRepository,
        ICommunityCommentRepository commentRepository,
        ISystemClock clock)
    {
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _clock = clock;
    }

    // ------------------------------------------------------------------
    //  Posts
    // ------------------------------------------------------------------

    public async Task<PagedResult<CommunityPostResponseDto>> GetPostsAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1)
            throw new ValidationException("page must be 1 or greater.");
        if (pageSize is < 1 or > MaxPageSize)
            throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}.");

        var (items, totalCount) = await _postRepository.GetPageAsync(page, pageSize, userId, ct);
        return new PagedResult<CommunityPostResponseDto>
        {
            Items = items.Select(MapPost).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<CommunityPostResponseDto> CreatePostAsync(
        Guid userId, CreateCommunityPostDto dto, CancellationToken ct = default)
    {
        var content = ValidateContent(dto.Content, MaxPostContentLength, "Post");
        var imageUrl = ValidateImageUrl(dto.ImageUrl);

        var post = new CommunityPost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Content = content,
            ImageUrl = imageUrl,
            CreatedAt = _clock.UtcNow
        };

        await _postRepository.AddAsync(post, ct);
        await _postRepository.SaveChangesAsync(ct);

        return new CommunityPostResponseDto
        {
            Id = post.Id,
            AuthorName = await GetDisplayNameAsync(userId, ct),
            Content = post.Content,
            ImageUrl = post.ImageUrl,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            CommentCount = 0,
            IsOwnedByCurrentUser = true
        };
    }

    public async Task<CommunityPostDetailDto> GetPostAsync(Guid userId, Guid postId, CancellationToken ct = default)
    {
        var post = await _postRepository.GetByIdAsync(postId, userId, ct)
            ?? throw new NotFoundException("Community post not found.");

        // Bounded first page; the full thread stays on the comments endpoint.
        var comments = await _commentRepository.GetFirstPageAsync(postId, userId, DetailCommentsPerPage, ct);

        return new CommunityPostDetailDto
        {
            Post = MapPost(post),
            Comments = comments.Select(MapComment).ToList()
        };
    }

    public async Task DeletePostAsync(Guid userId, Guid postId, CancellationToken ct = default)
    {
        var post = await GetOwnedPostAsync(userId, postId, ct);
        var now = _clock.UtcNow;

        post.IsDeleted = true;
        post.UpdatedAt = now;
        _postRepository.Update(post);

        // A deleted post must not leave publicly visible comments behind.
        await _commentRepository.SoftDeleteActiveByPostAsync(postId, now, ct);
        await _postRepository.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------
    //  Comments
    // ------------------------------------------------------------------

    public async Task<PagedResult<CommunityCommentResponseDto>> GetCommentsAsync(
        Guid userId, Guid postId, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1)
            throw new ValidationException("page must be 1 or greater.");
        if (pageSize is < 1 or > MaxPageSize)
            throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}.");

        await EnsurePostExistsAsync(postId, userId, ct);

        var (items, totalCount) = await _commentRepository.GetPageAsync(postId, userId, page, pageSize, ct);
        return new PagedResult<CommunityCommentResponseDto>
        {
            Items = items.Select(MapComment).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<CommunityCommentResponseDto> CreateCommentAsync(
        Guid userId, Guid postId, CreateCommunityCommentDto dto, CancellationToken ct = default)
    {
        await EnsurePostExistsAsync(postId, userId, ct);
        var content = ValidateContent(dto.Content, MaxCommentContentLength, "Comment");

        var comment = new CommunityComment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = userId,
            Content = content,
            CreatedAt = _clock.UtcNow
        };

        await _commentRepository.AddAsync(comment, ct);
        await _commentRepository.SaveChangesAsync(ct);

        return new CommunityCommentResponseDto
        {
            Id = comment.Id,
            AuthorName = await GetDisplayNameAsync(userId, ct),
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            IsOwnedByCurrentUser = true
        };
    }

    public async Task DeleteCommentAsync(Guid userId, Guid commentId, CancellationToken ct = default)
    {
        var comment = await _commentRepository.FindTrackedByIdAsync(commentId, ct)
            ?? throw new NotFoundException("Community comment not found.");

        if (comment.UserId != userId)
            throw new ForbiddenException("You do not have access to this community comment.");

        comment.IsDeleted = true;
        comment.UpdatedAt = _clock.UtcNow;
        _commentRepository.Update(comment);
        await _commentRepository.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------
    //  Ownership + existence (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<CommunityPost> GetOwnedPostAsync(Guid userId, Guid postId, CancellationToken ct)
    {
        var post = await _postRepository.FindTrackedByIdAsync(postId, ct)
            ?? throw new NotFoundException("Community post not found.");

        if (post.UserId != userId)
            throw new ForbiddenException("You do not have access to this community post.");

        return post;
    }

    private async Task EnsurePostExistsAsync(Guid postId, Guid userId, CancellationToken ct)
    {
        var exists = await _postRepository.GetByIdAsync(postId, userId, ct) is not null;
        if (!exists)
            throw new NotFoundException("Community post not found.");
    }

    /// <summary>Author display name only - never email, phone or password hash.</summary>
    private async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken ct)
    {
        var author = await _postRepository.GetAuthorNameAsync(userId, ct);
        return author ?? "Farmer";
    }

    // ------------------------------------------------------------------
    //  Validation
    // ------------------------------------------------------------------

    private static string ValidateContent(string? content, int maxLength, string noun)
    {
        var trimmed = content?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException($"{noun} content is required.");
        if (trimmed.Length > maxLength)
            throw new ValidationException($"{noun} content must be at most {maxLength} characters.");
        return trimmed;
    }

    /// <summary>
    /// Image support is a safe reference only: absolute HTTP/HTTPS URL. Never
    /// a filesystem path (C:\..., /etc/...), file:// URL or binary payload,
    /// and no storage is invented. Null/blank stays null.
    /// </summary>
    private static string? ValidateImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var url = imageUrl.Trim();
        if (url.Length > MaxImageUrlLength)
            throw new ValidationException($"Image URL must be at most {MaxImageUrlLength} characters.");

        if (url.Contains('\\') ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException("Image URL must be an absolute HTTP or HTTPS URL.");
        }

        return uri.ToString();
    }

    // ------------------------------------------------------------------
    //  Mapping
    // ------------------------------------------------------------------

    private static CommunityPostResponseDto MapPost(CommunityPostReadModel post) => new()
    {
        Id = post.Id,
        AuthorName = post.AuthorName,
        Content = post.Content,
        ImageUrl = post.ImageUrl,
        CreatedAt = post.CreatedAt,
        UpdatedAt = post.UpdatedAt,
        CommentCount = post.CommentCount,
        IsOwnedByCurrentUser = post.IsOwnedByCurrentUser
    };

    private static CommunityCommentResponseDto MapComment(CommunityCommentReadModel comment) => new()
    {
        Id = comment.Id,
        AuthorName = comment.AuthorName,
        Content = comment.Content,
        CreatedAt = comment.CreatedAt,
        IsOwnedByCurrentUser = comment.IsOwnedByCurrentUser
    };
}
