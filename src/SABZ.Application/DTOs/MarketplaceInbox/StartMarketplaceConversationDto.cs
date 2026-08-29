using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.MarketplaceInbox;

/// <summary>
/// Request body for POST /api/marketplace/listings/{listingId}/contact
/// ("Message Seller"). No sender/buyer/seller ids are accepted - identity
/// always comes from the JWT.
/// </summary>
public class StartMarketplaceConversationDto
{
    [Required(ErrorMessage = "Message is required.")]
    [MaxLength(2000, ErrorMessage = "Message must be at most 2000 characters.")]
    public string Message { get; set; } = string.Empty;
}
