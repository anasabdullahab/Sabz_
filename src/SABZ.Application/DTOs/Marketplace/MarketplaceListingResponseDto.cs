namespace SABZ.Application.DTOs.Marketplace;

/// <summary>
/// Marketplace listing detail. The seller's contact number is returned ONLY
/// when the requester owns the listing; for every other authenticated farmer
/// it stays null and contact happens through the private inbox
/// (POST /api/marketplace/listings/{listingId}/contact). The seller's email
/// is never exposed.
/// </summary>
public class MarketplaceListingResponseDto
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

    /// <summary>Present for the owner's own listing only; null otherwise.</summary>
    public string? ContactNumber { get; set; }

    public bool IsOwnedByCurrentUser { get; set; }
}
