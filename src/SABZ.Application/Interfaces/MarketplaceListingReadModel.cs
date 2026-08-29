namespace SABZ.Application.Interfaces;

/// <summary>
/// SQL projection for marketplace listing reads. Seller display name and the
/// ownership flag are computed in SQL - no entities, no N+1. ContactNumber is
/// projected for internal use only; the service returns it solely to the
/// listing owner.
/// </summary>
public record MarketplaceListingReadModel(
    Guid Id,
    string Title,
    string Category,
    string ListingType,
    string Description,
    decimal Price,
    string PriceUnit,
    string Location,
    string ContactNumber,
    string Condition,
    string Availability,
    string? ImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string SellerName,
    bool IsOwnedByCurrentUser);
