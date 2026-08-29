using SABZ.Application.DTOs.Marketplace;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Marketplace;

/// <summary>
/// Farmer marketplace service (Prompt 15). Listing discovery for
/// agricultural equipment (sale/rent) - a CONNECTION/DISCOVERY system only.
/// The price is an informational asking/rental rate; SABZ never processes
/// payments, orders, escrow or any financial transaction, and this service
/// never touches the financial ledger.
///
/// Design decisions:
/// - Identity always comes from the JWT (userId parameter is server-derived);
///   no DTO accepts a client-supplied user id and ownership can never change.
/// - Soft-delete: deleted listings vanish from feeds and detail reads while
///   private inbox history tied to the listing stays intact.
/// - Read paths are DB-side filtered/ordered/paginated SQL projections -
///   seller names computed in SQL, one round-trip per page, no N+1.
/// - Image support is a safe URL reference only (absolute HTTP/HTTPS),
///   reusing the Prompt 14 approach. No uploads, no binary, no cloud storage.
/// </summary>
public class MarketplaceService : IMarketplaceService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private const int MaxTitleLength = 150;
    private const int MaxCategoryLength = 50;
    private const int MaxDescriptionLength = 2000;
    private const int MaxLocationLength = 200;
    private const int MaxContactNumberLength = 30;
    private const int MaxAvailabilityLength = 100;
    private const int MaxImageUrlLength = 2048;

    /// <summary>Price is informational only, but bounded: greater than 0, at most one billion.</summary>
    private const decimal MaxPrice = 1_000_000_000m;

    /// <summary>Phone-like check kept permissive for legitimate Pakistani formats (+92..., 03xx-xxxxxxx).</summary>
    private static readonly System.Text.RegularExpressions.Regex ContactNumberShape =
        new(@"^\+?[0-9()\-\s.]{7,30}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly IMarketplaceListingRepository _listingRepository;
    private readonly ISystemClock _clock;

    public MarketplaceService(IMarketplaceListingRepository listingRepository, ISystemClock clock)
    {
        _listingRepository = listingRepository;
        _clock = clock;
    }

    // ------------------------------------------------------------------
    //  Read paths (SQL-side filtering, ordering, pagination, projection)
    // ------------------------------------------------------------------

    public async Task<MarketplacePagedResultDto> GetListingsAsync(
        Guid userId,
        int page,
        int pageSize,
        string? search,
        string? category,
        string? listingType,
        string? location,
        string? condition,
        CancellationToken ct = default)
    {
        ValidatePagination(page, pageSize);

        var (items, totalCount) = await _listingRepository.GetPageAsync(
            page, pageSize, userId, search, category, listingType, location, condition, ct);

        return new MarketplacePagedResultDto
        {
            Items = items.Select(MapSummary).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<MarketplaceListingResponseDto> GetListingAsync(Guid userId, Guid listingId, CancellationToken ct = default)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, userId, ct)
            ?? throw new NotFoundException("Marketplace listing not found.");

        return MapDetail(listing);
    }

    // ------------------------------------------------------------------
    //  Write paths (ownership always from the JWT)
    // ------------------------------------------------------------------

    public async Task<MarketplaceListingResponseDto> CreateListingAsync(
        Guid userId, CreateMarketplaceListingDto dto, CancellationToken ct = default)
    {
        var fields = ValidateFields(
            dto.Title, dto.Category, dto.ListingType, dto.Description, dto.Price,
            dto.PriceUnit, dto.Location, dto.ContactNumber, dto.Condition,
            dto.Availability, dto.ImageUrl);

        var listing = new MarketplaceListing
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = fields.Title,
            Category = fields.Category,
            ListingType = fields.ListingType,
            Description = fields.Description,
            Price = dto.Price,
            PriceUnit = fields.PriceUnit,
            Location = fields.Location,
            ContactNumber = fields.ContactNumber,
            Condition = fields.Condition,
            Availability = fields.Availability,
            ImageUrl = fields.ImageUrl,
            CreatedAt = _clock.UtcNow
        };

        await _listingRepository.AddAsync(listing, ct);
        await _listingRepository.SaveChangesAsync(ct);

        // Re-project through the same SQL read path used by the feed.
        return await GetListingAsync(userId, listing.Id, ct);
    }

    public async Task<MarketplaceListingResponseDto> UpdateListingAsync(
        Guid userId, Guid listingId, UpdateMarketplaceListingDto dto, CancellationToken ct = default)
    {
        var listing = await GetOwnedListingAsync(userId, listingId, ct);
        var fields = ValidateFields(
            dto.Title, dto.Category, dto.ListingType, dto.Description, dto.Price,
            dto.PriceUnit, dto.Location, dto.ContactNumber, dto.Condition,
            dto.Availability, dto.ImageUrl);

        // Full update semantics; ownership itself can never change.
        listing.Title = fields.Title;
        listing.Category = fields.Category;
        listing.ListingType = fields.ListingType;
        listing.Description = fields.Description;
        listing.Price = dto.Price;
        listing.PriceUnit = fields.PriceUnit;
        listing.Location = fields.Location;
        listing.ContactNumber = fields.ContactNumber;
        listing.Condition = fields.Condition;
        listing.Availability = fields.Availability;
        listing.ImageUrl = fields.ImageUrl;
        listing.UpdatedAt = _clock.UtcNow;

        _listingRepository.Update(listing);
        await _listingRepository.SaveChangesAsync(ct);

        return await GetListingAsync(userId, listing.Id, ct);
    }

    public async Task DeleteListingAsync(Guid userId, Guid listingId, CancellationToken ct = default)
    {
        var listing = await GetOwnedListingAsync(userId, listingId, ct);
        var now = _clock.UtcNow;

        // Soft delete: the listing disappears from all normal queries, but its
        // private inbox history (conversations + messages) stays intact.
        listing.IsDeleted = true;
        listing.DeletedAt = now;
        listing.UpdatedAt = now;

        _listingRepository.Update(listing);
        await _listingRepository.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------
    //  Ownership + existence (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<MarketplaceListing> GetOwnedListingAsync(Guid userId, Guid listingId, CancellationToken ct)
    {
        var listing = await _listingRepository.FindTrackedByIdAsync(listingId, ct)
            ?? throw new NotFoundException("Marketplace listing not found.");

        if (listing.UserId != userId)
            throw new ForbiddenException("You do not have access to this marketplace listing.");

        return listing;
    }

    // ------------------------------------------------------------------
    //  Validation
    // ------------------------------------------------------------------

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
            throw new ValidationException("page must be 1 or greater.");
        if (pageSize is < 1 or > MaxPageSize)
            throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}.");
    }

    private record ValidatedFields(
        string Title,
        string Category,
        string ListingType,
        string Description,
        string PriceUnit,
        string Location,
        string ContactNumber,
        string Condition,
        string Availability,
        string? ImageUrl);

    private static ValidatedFields ValidateFields(
        string? title,
        string? category,
        string? listingType,
        string? description,
        decimal price,
        string? priceUnit,
        string? location,
        string? contactNumber,
        string? condition,
        string? availability,
        string? imageUrl)
    {
        ValidatePrice(price);

        return new ValidatedFields(
            Title: ValidateRequired(title, MaxTitleLength, "Title"),
            Category: ValidateRequired(category, MaxCategoryLength, "Category"),
            ListingType: NormalizeControlled(listingType, MarketplaceListingTypes.All, "ListingType", "Sale or Rent"),
            Description: ValidateRequired(description, MaxDescriptionLength, "Description"),
            PriceUnit: NormalizeControlled(priceUnit, MarketplacePriceUnits.All, "PriceUnit", "Total, Day, Hour, Week or Month"),
            Location: ValidateRequired(location, MaxLocationLength, "Location"),
            ContactNumber: ValidateContactNumber(contactNumber),
            Condition: NormalizeControlled(condition, MarketplaceConditions.All, "Condition", "New or Used"),
            Availability: ValidateRequired(availability, MaxAvailabilityLength, "Availability"),
            ImageUrl: ValidateImageUrl(imageUrl));
    }

    private static string ValidateRequired(string? value, int maxLength, string noun)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException($"{noun} is required.");
        if (trimmed.Length > maxLength)
            throw new ValidationException($"{noun} must be at most {maxLength} characters.");
        return trimmed;
    }

    /// <summary>Case-insensitive membership against the controlled set; returns the canonical value.</summary>
    private static string NormalizeControlled(string? value, HashSet<string> allowed, string noun, string expected)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException($"{noun} is required.");

        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        throw new ValidationException($"{noun} must be one of: {expected}.");
    }

    private static decimal ValidatePrice(decimal price)
    {
        if (price <= 0m)
            throw new ValidationException("Price must be greater than 0.");
        if (price > MaxPrice)
            throw new ValidationException($"Price must be at most {MaxPrice:0}.");
        return price;
    }

    /// <summary>
    /// Phone-number-like validation without over-restricting legitimate
    /// Pakistani formats: optional leading +, digits with spaces/dashes/
    /// parentheses, 7-15 digits in total.
    /// </summary>
    private static string ValidateContactNumber(string? contactNumber)
    {
        var trimmed = contactNumber?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException("ContactNumber is required.");
        if (trimmed.Length > MaxContactNumberLength)
            throw new ValidationException($"ContactNumber must be at most {MaxContactNumberLength} characters.");

        if (!ContactNumberShape.IsMatch(trimmed))
            throw new ValidationException("ContactNumber must be a valid phone number.");

        var digitCount = trimmed.Count(char.IsDigit);
        if (digitCount is < 7 or > 15)
            throw new ValidationException("ContactNumber must contain between 7 and 15 digits.");

        return trimmed;
    }

    /// <summary>
    /// Image support is a safe reference only (Prompt 14 approach): absolute
    /// HTTP/HTTPS URL. Never a filesystem path (C:\..., /etc/...), file://
    /// URL or UNC path; nothing is downloaded and no storage is invented.
    /// Null/blank stays null.
    /// </summary>
    private static string? ValidateImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var url = imageUrl.Trim();
        if (url.Length > MaxImageUrlLength)
            throw new ValidationException($"Image URL must be at most {MaxImageUrlLength} characters.");

        if (url.Contains('\\') ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException("Image URL must be an absolute HTTP or HTTPS URL.");
        }

        return uri.ToString();
    }

    // ------------------------------------------------------------------
    //  Mapping
    // ------------------------------------------------------------------

    private static MarketplaceListingSummaryDto MapSummary(MarketplaceListingReadModel listing) => new()
    {
        Id = listing.Id,
        Title = listing.Title,
        Category = listing.Category,
        ListingType = listing.ListingType,
        Description = listing.Description,
        Price = listing.Price,
        PriceUnit = listing.PriceUnit,
        Location = listing.Location,
        Condition = listing.Condition,
        Availability = listing.Availability,
        ImageUrl = listing.ImageUrl,
        CreatedAt = listing.CreatedAt,
        UpdatedAt = listing.UpdatedAt,
        SellerName = listing.SellerName,
        IsOwnedByCurrentUser = listing.IsOwnedByCurrentUser
        // Deliberately no ContactNumber in the public feed.
    };

    private static MarketplaceListingResponseDto MapDetail(MarketplaceListingReadModel listing) => new()
    {
        Id = listing.Id,
        Title = listing.Title,
        Category = listing.Category,
        ListingType = listing.ListingType,
        Description = listing.Description,
        Price = listing.Price,
        PriceUnit = listing.PriceUnit,
        Location = listing.Location,
        Condition = listing.Condition,
        Availability = listing.Availability,
        ImageUrl = listing.ImageUrl,
        CreatedAt = listing.CreatedAt,
        UpdatedAt = listing.UpdatedAt,
        SellerName = listing.SellerName,
        // Contact number is shown to the owner only; everyone else uses the
        // private inbox contact action instead.
        ContactNumber = listing.IsOwnedByCurrentUser ? listing.ContactNumber : null,
        IsOwnedByCurrentUser = listing.IsOwnedByCurrentUser
    };
}
