namespace SABZ.Application.DTOs.Marketplace;

/// <summary>
/// DB-side pagination envelope for the marketplace listing feed
/// (page/pageSize defaults 1/20, maximum pageSize 50).
/// </summary>
public class MarketplacePagedResultDto
{
    public List<MarketplaceListingSummaryDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
