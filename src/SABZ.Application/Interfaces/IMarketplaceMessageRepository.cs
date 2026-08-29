using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Private message persistence (Prompt 15). Messages are readable only
/// through a conversation whose membership the inbox service has already
/// verified against the JWT identity.
/// </summary>
public interface IMarketplaceMessageRepository
{
    /// <summary>Oldest-first, DB-side paginated message thread with sender names computed in SQL.</summary>
    Task<(List<MarketplaceMessageReadModel> Items, int TotalCount)> GetPageAsync(
        Guid conversationId, Guid currentUserId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Display name only - never email, phone or password hash.</summary>
    Task<string?> GetSenderNameAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(MarketplaceMessage message, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
