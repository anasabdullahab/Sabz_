namespace SABZ.Application.Interfaces;

/// <summary>
/// SQL projection for the conversation detail header: listing context is
/// preserved even after the listing is soft-deleted. Participant ids stay
/// internal (role + IsOwnMessage computation) and are never serialized.
/// </summary>
public record MarketplaceConversationDetailReadModel(
    Guid ConversationId,
    Guid ListingId,
    string ListingTitle,
    string ListingType,
    decimal ListingPrice,
    string ListingPriceUnit,
    Guid BuyerUserId,
    Guid SellerUserId,
    string BuyerName,
    string SellerName);
