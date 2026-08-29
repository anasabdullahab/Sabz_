using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.MarketplaceInbox;

/// <summary>
/// Request body for POST /api/marketplace/inbox/{conversationId}/messages.
/// The sender always comes from the JWT; SenderUserId is never accepted.
/// </summary>
public class SendMarketplaceMessageDto
{
    [Required(ErrorMessage = "Message is required.")]
    [MaxLength(2000, ErrorMessage = "Message must be at most 2000 characters.")]
    public string Message { get; set; } = string.Empty;
}
