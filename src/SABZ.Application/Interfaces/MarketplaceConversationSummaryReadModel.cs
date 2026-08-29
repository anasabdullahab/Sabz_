namespace SABZ.Application.Interfaces;

/// <summary>
/// SQL projection for one inbox row. The listing title comes from the
/// listing even when it is soft-deleted (private history stays readable),
/// and the latest message preview/timestamp are computed in SQL.
/// BuyerUserId/SellerUserId stay internal for role computation - they are
/// never serialized into any DTO.
/// </summary>
public record MarketplaceConversationSummaryReadModel(
    Guid ConversationId,
    Guid ListingId,
    string ListingTitle,
    Guid BuyerUserId,
    Guid SellerUserId,
    string BuyerName,
    string SellerName,
    string? LatestMessagePreview,
    DateTime? LatestMessageAt);
