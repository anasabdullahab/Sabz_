namespace SABZ.Domain.Entities;

/// <summary>
/// Private farmer-to-farmer conversation about one marketplace listing
/// (Prompt 15). Exactly one conversation exists per
/// (ListingId, BuyerUserId, SellerUserId) combination - enforced by a unique
/// database index - so "Message Seller" always reuses the existing thread.
///
/// Only the buyer or the seller may ever read or write messages in a
/// conversation; every inbox endpoint verifies membership against the JWT
/// identity. There is no public message feed.
///
/// Conversations and their messages are preserved even when the listing is
/// soft-deleted: deleting a listing never destroys private message history.
/// </summary>
public class MarketplaceConversation
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Guid BuyerUserId { get; set; }
    public Guid SellerUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Bumped on every new message; drives the newest-first inbox ordering.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    public MarketplaceListing Listing { get; set; } = null!;
    public User BuyerUser { get; set; } = null!;
    public User SellerUser { get; set; } = null!;
    public ICollection<MarketplaceMessage> Messages { get; set; } = new List<MarketplaceMessage>();
}
