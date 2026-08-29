using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Community;
using SABZ.Application.Interfaces;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

/// <summary>
/// Farmer community (Prompt 14): agriculture-focused posts and comments for
/// all authenticated SABZ farmers. A discussion feature, not a social-media
/// clone - no likes, followers, messaging or AI moderation, and no
/// notifications are produced by this feature.
///
/// All endpoints require authentication (reads included, consistent with the
/// rest of SABZ). Ownership always comes from the JWT user; no endpoint ever
/// accepts a client-supplied user id.
/// </summary>
[ApiController]
[Authorize]
public class CommunityController : ControllerBase
{
    private readonly ICommunityService _communityService;

    public CommunityController(ICommunityService communityService)
    {
        _communityService = communityService;
    }

    /// <summary>Community feed, newest first, DB-side paginated (page=1, pageSize=20, max 50).</summary>
    [HttpGet("api/community/posts")]
    public async Task<IActionResult> GetPosts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _communityService.GetPostsAsync(userId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Create a community post (content required, optional safe HTTP/HTTPS image URL).</summary>
    [HttpPost("api/community/posts")]
    public async Task<IActionResult> CreatePost([FromBody] CreateCommunityPostDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _communityService.CreatePostAsync(userId, dto, ct);
        return Ok(result);
    }

    /// <summary>One community post with a bounded oldest-first first page of its comments.</summary>
    [HttpGet("api/community/posts/{postId:guid}")]
    public async Task<IActionResult> GetPost(Guid postId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _communityService.GetPostAsync(userId, postId, ct);
        return Ok(result);
    }

    /// <summary>Soft-delete a community post (author only); also hides its comments.</summary>
    [HttpDelete("api/community/posts/{postId:guid}")]
    public async Task<IActionResult> DeletePost(Guid postId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _communityService.DeletePostAsync(userId, postId, ct);
        return NoContent();
    }

    /// <summary>Visible comments of a post, oldest first, DB-side paginated (page=1, pageSize=20, max 50).</summary>
    [HttpGet("api/community/posts/{postId:guid}/comments")]
    public async Task<IActionResult> GetComments(
        Guid postId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var comments = await _communityService.GetCommentsAsync(userId, postId, page, pageSize, ct);
        return Ok(comments);
    }

    /// <summary>Add a comment to a community post (content required).</summary>
    [HttpPost("api/community/posts/{postId:guid}/comments")]
    public async Task<IActionResult> CreateComment(Guid postId, [FromBody] CreateCommunityCommentDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _communityService.CreateCommentAsync(userId, postId, dto, ct);
        return Ok(result);
    }

    /// <summary>Soft-delete a community comment (author only).</summary>
    [HttpDelete("api/community/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _communityService.DeleteCommentAsync(userId, commentId, ct);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new SABZ.Domain.Exceptions.AuthenticationException("Invalid token.");
        return userId;
    }

    private static Dictionary<string, string[]> ValidateModel(object model)
    {
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        if (results.Count == 0)
            return new Dictionary<string, string[]>();

        return results
            .Where(r => r != ValidationResult.Success)
            .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "Error")
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.ErrorMessage ?? "Invalid value.").ToArray());
    }
}
