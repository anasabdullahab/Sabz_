namespace SABZ.Application.DTOs.MarketplaceInbox;

/// <summary>
/// DB-side pagination envelope for the private inbox
/// (page/pageSize defaults 1/20, maximum pageSize 50, newest activity first).
/// </summary>
public class MarketplaceInboxPagedResultDto
{
    public List<MarketplaceConversationSummaryDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
