using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Community;

/// <summary>Request body for POST /api/community/posts.</summary>
public class CreateCommunityPostDto
{
    /// <summary>Post text; required, non-whitespace, at most 2000 characters.</summary>
    [Required(ErrorMessage = "Content is required.")]
    [MaxLength(2000, ErrorMessage = "Post content must be at most 2000 characters.")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional safe image reference: an absolute HTTP/HTTPS URL only.
    /// Filesystem paths, file:// URLs and binary payloads are rejected.
    /// </summary>
    [MaxLength(2048, ErrorMessage = "Image URL must be at most 2048 characters.")]
    public string? ImageUrl { get; set; }
}
