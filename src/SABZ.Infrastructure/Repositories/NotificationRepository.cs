using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly SabzDbContext _context;

    public NotificationRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<List<Notification>> GetByUserIdAsync(Guid userId, int take, CancellationToken ct = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<Notification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task<HashSet<Guid>> GetExistingReferenceIdsAsync(Guid userId, string referenceType, string category, CancellationToken ct = default)
    {
        var referenceIds = await _context.Notifications
            .Where(n => n.UserId == userId && n.ReferenceType == referenceType && n.Category == category)
            .Select(n => n.ReferenceId)
            .ToListAsync(ct);

        return new HashSet<Guid>(referenceIds);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(notification, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> SaveChangesGuardedAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Concurrency race: another request already inserted the same
            // (user, reference, category) notification and the unique index
            // rejected this one. Detach the pending notification entries so the
            // context stays usable and report "nothing created".
            foreach (var entry in _context.ChangeTracker.Entries<Notification>()
                         .Where(e => e.State == EntityState.Added))
            {
                entry.State = EntityState.Detached;
            }
            return false;
        }
    }

    public void Update(Notification notification)
    {
        _context.Notifications.Update(notification);
    }
}
