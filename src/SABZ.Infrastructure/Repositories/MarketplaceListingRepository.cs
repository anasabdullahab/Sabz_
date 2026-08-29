using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class MarketplaceListingRepository : IMarketplaceListingRepository
{
    private readonly SabzDbContext _context;

    public MarketplaceListingRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<(List<MarketplaceListingReadModel> Items, int TotalCount)> GetPageAsync(
        int page,
        int pageSize,
        Guid currentUserId,
        string? search,
        string? category,
        string? listingType,
        string? location,
        string? condition,
        CancellationToken ct = default)
    {
        // Soft-deleted listings never appear in any normal marketplace query.
        var listings = _context.MarketplaceListings.AsNoTracking().Where(l => !l.IsDeleted);

        // All filtering stays in SQL; controlled values are already validated
        // and normalized by the service layer.
        var searchTerm = TrimToNull(search);
        if (searchTerm is not null)
        {
            var like = $"%{EscapeLike(searchTerm)}%";
            listings = listings.Where(l =>
                EF.Functions.Like(l.Title, like) || EF.Functions.Like(l.Description, like));
        }

        var categoryTerm = TrimToNull(category);
        if (categoryTerm is not null)
            listings = listings.Where(l => EF.Functions.Like(l.Category, $"%{EscapeLike(categoryTerm)}%"));

        var listingTypeTerm = TrimToNull(listingType);
        if (listingTypeTerm is not null)
            listings = listings.Where(l => l.ListingType == listingTypeTerm);

        var locationTerm = TrimToNull(location);
        if (locationTerm is not null)
            listings = listings.Where(l => EF.Functions.Like(l.Location, $"%{EscapeLike(locationTerm)}%"));

        var conditionTerm = TrimToNull(condition);
        if (conditionTerm is not null)
            listings = listings.Where(l => l.Condition == conditionTerm);

        var totalCount = await listings.CountAsync(ct);

        // Seller name and ownership flag are computed in SQL: one round-trip
        // per page, no entities, no N+1. No trending/ranking - plain
        // newest-first.
        var items = await listings
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new MarketplaceListingReadModel(
                l.Id,
                l.Title,
                l.Category,
                l.ListingType,
                l.Description,
                l.Price,
                l.PriceUnit,
                l.Location,
                l.ContactNumber,
                l.Condition,
                l.Availability,
                l.ImageUrl,
                l.CreatedAt,
                l.UpdatedAt,
                l.User.FullName,
                l.UserId == currentUserId))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<MarketplaceListingReadModel?> GetByIdAsync(Guid listingId, Guid currentUserId, CancellationToken ct = default)
    {
        return await _context.MarketplaceListings.AsNoTracking()
            .Where(l => l.Id == listingId && !l.IsDeleted)
            .Select(l => new MarketplaceListingReadModel(
                l.Id,
                l.Title,
                l.Category,
                l.ListingType,
                l.Description,
                l.Price,
                l.PriceUnit,
                l.Location,
                l.ContactNumber,
                l.Condition,
                l.Availability,
                l.ImageUrl,
                l.CreatedAt,
                l.UpdatedAt,
                l.User.FullName,
                l.UserId == currentUserId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<MarketplaceListing?> FindTrackedByIdAsync(Guid listingId, CancellationToken ct = default)
    {
        return await _context.MarketplaceListings
            .FirstOrDefaultAsync(l => l.Id == listingId && !l.IsDeleted, ct);
    }

    public async Task AddAsync(MarketplaceListing listing, CancellationToken ct = default)
    {
        await _context.MarketplaceListings.AddAsync(listing, ct);
    }

    public void Update(MarketplaceListing listing)
    {
        _context.MarketplaceListings.Update(listing);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Escapes LIKE wildcards so user input is matched literally.</summary>
    private static string EscapeLike(string value) => value
        .Replace("[", "[[]")
        .Replace("%", "[%]")
        .Replace("_", "[_]");
}
