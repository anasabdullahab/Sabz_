using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Marketplace;

/// <summary>
/// Request body for PUT /api/marketplace/listings/{listingId}. Full update
/// semantics: every field is required and re-validated. Ownership cannot be
/// changed - the owner always stays the JWT-authenticated seller.
/// </summary>
public class UpdateMarketplaceListingDto
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(150, ErrorMessage = "Title must be at most 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [MaxLength(50, ErrorMessage = "Category must be at most 50 characters.")]
    public string Category { get; set; } = string.Empty;

    /// <summary>Controlled value: Sale or Rent.</summary>
    [Required(ErrorMessage = "ListingType is required.")]
    [MaxLength(10, ErrorMessage = "ListingType must be at most 10 characters.")]
    public string ListingType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(2000, ErrorMessage = "Description must be at most 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Must be greater than 0 and at most 1,000,000,000 (validated server-side).</summary>
    public decimal Price { get; set; }

    /// <summary>Controlled value: Total, Day, Hour, Week or Month.</summary>
    [Required(ErrorMessage = "PriceUnit is required.")]
    [MaxLength(20, ErrorMessage = "PriceUnit must be at most 20 characters.")]
    public string PriceUnit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required.")]
    [MaxLength(200, ErrorMessage = "Location must be at most 200 characters.")]
    public string Location { get; set; } = string.Empty;

    /// <summary>Never exposed in the public listing feed.</summary>
    [Required(ErrorMessage = "ContactNumber is required.")]
    [MaxLength(30, ErrorMessage = "ContactNumber must be at most 30 characters.")]
    public string ContactNumber { get; set; } = string.Empty;

    /// <summary>Controlled value: New or Used.</summary>
    [Required(ErrorMessage = "Condition is required.")]
    [MaxLength(10, ErrorMessage = "Condition must be at most 10 characters.")]
    public string Condition { get; set; } = string.Empty;

    [Required(ErrorMessage = "Availability is required.")]
    [MaxLength(100, ErrorMessage = "Availability must be at most 100 characters.")]
    public string Availability { get; set; } = string.Empty;

    /// <summary>Optional safe image reference: absolute HTTP/HTTPS URL only.</summary>
    [MaxLength(2048, ErrorMessage = "Image URL must be at most 2048 characters.")]
    public string? ImageUrl { get; set; }
}
