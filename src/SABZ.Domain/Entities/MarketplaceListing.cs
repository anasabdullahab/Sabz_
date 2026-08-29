namespace SABZ.Domain.Entities;

/// <summary>
/// Farmer marketplace listing (Prompt 15). A farmer-to-farmer discovery
/// record for agricultural equipment (or similar farm items) offered for
/// sale or rent.
///
/// The marketplace is a CONNECTION/DISCOVERY system only: the price is an
/// informational asking/rental rate supplied by the farmer. SABZ never
/// processes payments, orders, escrow or any financial transaction - the
/// actual arrangement happens privately between the two farmers outside SABZ.
///
/// UserId is never accepted from clients; it is always derived server-side
/// from the JWT identity and never exposed in API responses. Listings are
/// soft-deleted (<see cref="IsDeleted"/>) so deleted listings disappear from
/// feeds and detail reads while private inbox history tied to the listing
/// remains intact.
/// </summary>
public class MarketplaceListing
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public required string Title { get; set; }
    public required string Category { get; set; }

    /// <summary>Controlled value: Sale or Rent (see <see cref="MarketplaceListingTypes"/>).</summary>
    public required string ListingType { get; set; }

    public required string Description { get; set; }

    /// <summary>Informational asking/rental rate only - never charged or processed by SABZ.</summary>
    public decimal Price { get; set; }

    /// <summary>Controlled value: Total, Day, Hour, Week or Month (see <see cref="MarketplacePriceUnits"/>).</summary>
    public required string PriceUnit { get; set; }

    /// <summary>Free-text place name; GPS coordinates are not required for this feature.</summary>
    public required string Location { get; set; }

    /// <summary>
    /// Seller-supplied phone number. Shown only on the owner's own listing
    /// detail; never exposed in the public listing feed.
    /// </summary>
    public required string ContactNumber { get; set; }

    /// <summary>Controlled value: New or Used (see <see cref="MarketplaceConditions"/>).</summary>
    public required string Condition { get; set; }

    public required string Availability { get; set; }

    /// <summary>Optional safe image reference (HTTP/HTTPS URL only, never a filesystem path).</summary>
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<MarketplaceConversation> Conversations { get; set; } = new List<MarketplaceConversation>();
}
