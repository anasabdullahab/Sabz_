using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class MarketplaceMessageRepository : IMarketplaceMessageRepository
{
    private readonly SabzDbContext _context;

    public MarketplaceMessageRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<(List<MarketplaceMessageReadModel> Items, int TotalCount)> GetPageAsync(
        Guid conversationId, Guid currentUserId, int page, int pageSize, CancellationToken ct = default)
    {
        // Membership of the conversation is verified by the inbox service
        // before this is ever called; soft-deleted messages stay hidden.
        var messages = _context.MarketplaceMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted);

        var totalCount = await messages.CountAsync(ct);

        // Oldest-first chat order; sender name and ownership flag computed
        // in SQL - one round-trip per page, no N+1.
        var items = await messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MarketplaceMessageReadModel(
                m.Id,
                m.SenderUser.FullName,
                m.Content,
                m.CreatedAt,
                m.SenderUserId == currentUserId))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<string?> GetSenderNameAsync(Guid userId, CancellationToken ct = default)
    {
        // Display name only - never email, phone or password hash.
        return await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(MarketplaceMessage message, CancellationToken ct = default)
    {
        await _context.MarketplaceMessages.AddAsync(message, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
