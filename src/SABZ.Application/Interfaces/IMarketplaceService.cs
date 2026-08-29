using SABZ.Application.DTOs.Marketplace;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Farmer marketplace service (Prompt 15): listing discovery for agricultural
/// equipment (sale/rent). A CONNECTION/DISCOVERY system only - SABZ never
/// processes payments, orders, escrow or any financial transaction; the
/// actual arrangement happens privately between farmers outside SABZ.
/// </summary>
public interface IMarketplaceService
{
    Task<MarketplacePagedResultDto> GetListingsAsync(
        Guid userId,
        int page,
        int pageSize,
        string? search,
        string? category,
        string? listingType,
        string? location,
        string? condition,
        CancellationToken ct = default);

    Task<MarketplaceListingResponseDto> CreateListingAsync(Guid userId, CreateMarketplaceListingDto dto, CancellationToken ct = default);

    Task<MarketplaceListingResponseDto> GetListingAsync(Guid userId, Guid listingId, CancellationToken ct = default);

    Task<MarketplaceListingResponseDto> UpdateListingAsync(Guid userId, Guid listingId, UpdateMarketplaceListingDto dto, CancellationToken ct = default);

    Task DeleteListingAsync(Guid userId, Guid listingId, CancellationToken ct = default);
}
