using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Community;

/// <summary>Request body for POST /api/community/posts/{postId}/comments.</summary>
public class CreateCommunityCommentDto
{
    /// <summary>Comment text; required, non-whitespace, at most 1000 characters.</summary>
    [Required(ErrorMessage = "Content is required.")]
    [MaxLength(1000, ErrorMessage = "Comment content must be at most 1000 characters.")]
    public string Content { get; set; } = string.Empty;
}
