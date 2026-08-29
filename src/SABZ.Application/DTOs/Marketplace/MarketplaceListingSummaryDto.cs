namespace SABZ.Application.DTOs.Marketplace;

/// <summary>
/// Public marketplace feed row. Deliberately excludes the seller's contact
/// number and any internal ownership id - the feed is discovery only, and
/// contact happens through the private inbox.
/// </summary>
public class MarketplaceListingSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PriceUnit { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Seller display name only - never email, phone or id.</summary>
    public string SellerName { get; set; } = string.Empty;

    public bool IsOwnedByCurrentUser { get; set; }
}
