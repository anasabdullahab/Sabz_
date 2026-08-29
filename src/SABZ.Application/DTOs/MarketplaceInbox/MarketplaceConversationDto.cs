using SABZ.Application.DTOs.Community;

namespace SABZ.Application.DTOs.MarketplaceInbox;

/// <summary>
/// Full conversation view for one participant: listing context, both
/// participants' display names and a DB-side paginated message thread.
/// Only BuyerUserId/SellerUserId holders ever receive this DTO; no user ids,
/// emails or phone numbers are included.
/// </summary>
public class MarketplaceConversationDto
{
    public Guid ConversationId { get; set; }
    public Guid ListingId { get; set; }

    /// <summary>Listing title is preserved even after the listing is soft-deleted.</summary>
    public string ListingTitle { get; set; } = string.Empty;

    public string ListingType { get; set; } = string.Empty;

    /// <summary>Informational asking/rental rate - SABZ never processes payments.</summary>
    public decimal ListingPrice { get; set; }
    public string ListingPriceUnit { get; set; } = string.Empty;

    public string BuyerName { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;

    /// <summary>The current user's role in this conversation: Buyer or Seller.</summary>
    public string CurrentUserRole { get; set; } = string.Empty;

    /// <summary>Messages oldest-first, DB-side paginated (reuses the Prompt 14 envelope).</summary>
    public PagedResult<MarketplaceMessageDto> Messages { get; set; } = new();
}
