using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Marketplace listing persistence (Prompt 15). All reads are AsNoTracking
/// SQL projections; soft-deleted listings never appear in normal queries.
/// </summary>
public interface IMarketplaceListingRepository
{
    /// <summary>
    /// Filtered, newest-first, DB-side paginated public feed. Search matches
    /// title/description; category and location are case-insensitive
    /// contains filters; listingType and condition are controlled values.
    /// </summary>
    Task<(List<MarketplaceListingReadModel> Items, int TotalCount)> GetPageAsync(
        int page,
        int pageSize,
        Guid currentUserId,
        string? search,
        string? category,
        string? listingType,
        string? location,
        string? condition,
        CancellationToken ct = default);

    /// <summary>One active listing projected for detail reads; null when unknown/deleted.</summary>
    Task<MarketplaceListingReadModel?> GetByIdAsync(Guid listingId, Guid currentUserId, CancellationToken ct = default);

    /// <summary>Tracked active entity for write paths (update/delete/contact); null when unknown/deleted.</summary>
    Task<MarketplaceListing?> FindTrackedByIdAsync(Guid listingId, CancellationToken ct = default);

    Task AddAsync(MarketplaceListing listing, CancellationToken ct = default);
    void Update(MarketplaceListing listing);
    Task SaveChangesAsync(CancellationToken ct = default);
}
