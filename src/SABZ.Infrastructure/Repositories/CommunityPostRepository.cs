using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class CommunityPostRepository : ICommunityPostRepository
{
    private readonly SabzDbContext _context;

    public CommunityPostRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<(List<CommunityPostReadModel> Items, int TotalCount)> GetPageAsync(
        int page, int pageSize, Guid currentUserId, CancellationToken ct = default)
    {
        // Soft-deleted rows never appear in any normal community query.
        var posts = _context.CommunityPosts.AsNoTracking().Where(p => !p.IsDeleted);

        var totalCount = await posts.CountAsync(ct);

        // Author name and visible comment count are computed in SQL: one
        // round-trip per page, no entities, no N+1.
        var items = await posts
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new CommunityPostReadModel(
                p.Id,
                p.User.FullName,
                p.Content,
                p.ImageUrl,
                p.CreatedAt,
                p.UpdatedAt,
                p.Comments.Count(c => !c.IsDeleted),
                p.UserId == currentUserId))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<CommunityPostReadModel?> GetByIdAsync(Guid postId, Guid currentUserId, CancellationToken ct = default)
    {
        return await _context.CommunityPosts.AsNoTracking()
            .Where(p => p.Id == postId && !p.IsDeleted)
            .Select(p => new CommunityPostReadModel(
                p.Id,
                p.User.FullName,
                p.Content,
                p.ImageUrl,
                p.CreatedAt,
                p.UpdatedAt,
                p.Comments.Count(c => !c.IsDeleted),
                p.UserId == currentUserId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CommunityPost?> FindTrackedByIdAsync(Guid postId, CancellationToken ct = default)
    {
        return await _context.CommunityPosts
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);
    }

    public async Task<string?> GetAuthorNameAsync(Guid userId, CancellationToken ct = default)
    {
        // Display name only - never email, phone or password hash.
        return await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(CommunityPost post, CancellationToken ct = default)
    {
        await _context.CommunityPosts.AddAsync(post, ct);
    }

    public void Update(CommunityPost post)
    {
        _context.CommunityPosts.Update(post);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
