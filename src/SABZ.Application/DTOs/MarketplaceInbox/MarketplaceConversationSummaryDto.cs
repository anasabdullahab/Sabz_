namespace SABZ.Application.DTOs.MarketplaceInbox;

/// <summary>
/// One inbox row for the authenticated farmer. Only display names and the
/// listing title - never user ids, emails or phone numbers.
/// </summary>
public class MarketplaceConversationSummaryDto
{
    public Guid ConversationId { get; set; }
    public Guid ListingId { get; set; }
    public string ListingTitle { get; set; } = string.Empty;

    /// <summary>Display name of the other participant (buyer sees seller, seller sees buyer).</summary>
    public string OtherParticipantName { get; set; } = string.Empty;

    /// <summary>Content of the most recent message, if any.</summary>
    public string? LatestMessagePreview { get; set; }
    public DateTime? LatestMessageAt { get; set; }

    /// <summary>The current user's role in this conversation: Buyer or Seller.</summary>
    public string Role { get; set; } = string.Empty;
}
