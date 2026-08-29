using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Private inbox conversation persistence (Prompt 15). Conversations are
/// readable only by their two participants; the repositories themselves do
/// no authorization - the inbox service enforces membership against the JWT
/// identity before every read/write.
/// </summary>
public interface IMarketplaceConversationRepository
{
    /// <summary>
    /// The authenticated farmer's conversations, newest activity first,
    /// DB-side paginated, with listing title and latest message computed
    /// in SQL.
    /// </summary>
    Task<(List<MarketplaceConversationSummaryReadModel> Items, int TotalCount)> GetInboxPageAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Conversation header projection (participants + listing context); null when unknown/deleted.</summary>
    Task<MarketplaceConversationDetailReadModel?> GetDetailAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>Tracked active entity for write paths; null when unknown/deleted.</summary>
    Task<MarketplaceConversation?> FindTrackedByIdAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Reuses the unique (ListingId, BuyerUserId, SellerUserId) identity so
    /// "Message Seller" never creates duplicate conversations.
    /// </summary>
    Task<MarketplaceConversation?> FindByParticipantsAsync(
        Guid listingId, Guid buyerUserId, Guid sellerUserId, CancellationToken ct = default);

    Task AddAsync(MarketplaceConversation conversation, CancellationToken ct = default);
    void Update(MarketplaceConversation conversation);
    Task SaveChangesAsync(CancellationToken ct = default);
}
