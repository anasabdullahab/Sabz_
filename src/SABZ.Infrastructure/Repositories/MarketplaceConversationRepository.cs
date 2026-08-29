using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class MarketplaceConversationRepository : IMarketplaceConversationRepository
{
    private readonly SabzDbContext _context;

    public MarketplaceConversationRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<(List<MarketplaceConversationSummaryReadModel> Items, int TotalCount)> GetInboxPageAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        // Participants only - a farmer sees conversations where they are the
        // buyer or the seller. There is no public message feed.
        var conversations = _context.MarketplaceConversations.AsNoTracking()
            .Where(c => !c.IsDeleted && (c.BuyerUserId == userId || c.SellerUserId == userId));

        var totalCount = await conversations.CountAsync(ct);

        // Listing title, participant names and the latest message are all
        // computed in SQL. The listing title survives listing soft-deletion
        // so private history stays readable.
        var items = await conversations
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new MarketplaceConversationSummaryReadModel(
                c.Id,
                c.ListingId,
                c.Listing.Title,
                c.BuyerUserId,
                c.SellerUserId,
                c.BuyerUser.FullName,
                c.SellerUser.FullName,
                c.Messages.Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                c.Messages.Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .Select(m => (DateTime?)m.CreatedAt)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<MarketplaceConversationDetailReadModel?> GetDetailAsync(Guid conversationId, CancellationToken ct = default)
    {
        return await _context.MarketplaceConversations.AsNoTracking()
            .Where(c => c.Id == conversationId && !c.IsDeleted)
            .Select(c => new MarketplaceConversationDetailReadModel(
                c.Id,
                c.ListingId,
                c.Listing.Title,
                c.Listing.ListingType,
                c.Listing.Price,
                c.Listing.PriceUnit,
                c.BuyerUserId,
                c.SellerUserId,
                c.BuyerUser.FullName,
                c.SellerUser.FullName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<MarketplaceConversation?> FindTrackedByIdAsync(Guid conversationId, CancellationToken ct = default)
    {
        return await _context.MarketplaceConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && !c.IsDeleted, ct);
    }

    public async Task<MarketplaceConversation?> FindByParticipantsAsync(
        Guid listingId, Guid buyerUserId, Guid sellerUserId, CancellationToken ct = default)
    {
        return await _context.MarketplaceConversations
            .FirstOrDefaultAsync(c =>
                c.ListingId == listingId &&
                c.BuyerUserId == buyerUserId &&
                c.SellerUserId == sellerUserId &&
                !c.IsDeleted, ct);
    }

    public async Task AddAsync(MarketplaceConversation conversation, CancellationToken ct = default)
    {
        await _context.MarketplaceConversations.AddAsync(conversation, ct);
    }

    public void Update(MarketplaceConversation conversation)
    {
        _context.MarketplaceConversations.Update(conversation);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
