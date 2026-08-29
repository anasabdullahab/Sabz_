using SABZ.Application.DTOs.MarketplaceInbox;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Private farmer-to-farmer inbox for marketplace listings (Prompt 15).
/// Only the two conversation participants may read or write messages;
/// membership is always verified against the JWT identity. No public
/// message feed, no notifications, no background jobs.
/// </summary>
public interface IMarketplaceInboxService
{
    Task<MarketplaceInboxPagedResultDto> GetInboxAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<MarketplaceConversationDto> GetConversationAsync(
        Guid userId, Guid conversationId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>"Message Seller": reuses the existing conversation for the listing/buyer/seller trio.</summary>
    Task<MarketplaceConversationDto> ContactSellerAsync(Guid userId, Guid listingId, StartMarketplaceConversationDto dto, CancellationToken ct = default);

    Task<MarketplaceMessageDto> SendMessageAsync(Guid userId, Guid conversationId, SendMarketplaceMessageDto dto, CancellationToken ct = default);
}
