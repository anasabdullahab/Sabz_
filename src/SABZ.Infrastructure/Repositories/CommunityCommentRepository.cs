using Microsoft.EntityFrameworkCore;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Infrastructure.Persistence;

namespace SABZ.Infrastructure.Repositories;

public class CommunityCommentRepository : ICommunityCommentRepository
{
    private readonly SabzDbContext _context;

    public CommunityCommentRepository(SabzDbContext context)
    {
        _context = context;
    }

    public async Task<(List<CommunityCommentReadModel> Items, int TotalCount)> GetPageAsync(
        Guid postId, Guid currentUserId, int page, int pageSize, CancellationToken ct = default)
    {
        var comments = _context.CommunityComments.AsNoTracking()
            .Where(c => c.PostId == postId && !c.IsDeleted);

        var totalCount = await comments.CountAsync(ct);

        // Author display name computed in SQL: no entities, no N+1.
        var items = await comments
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommunityCommentReadModel(
                c.Id,
                c.User.FullName,
                c.Content,
                c.CreatedAt,
                c.UserId == currentUserId))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<List<CommunityCommentReadModel>> GetFirstPageAsync(
        Guid postId, Guid currentUserId, int take, CancellationToken ct = default)
    {
        return await _context.CommunityComments.AsNoTracking()
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Take(take)
            .Select(c => new CommunityCommentReadModel(
                c.Id,
                c.User.FullName,
                c.Content,
                c.CreatedAt,
                c.UserId == currentUserId))
            .ToListAsync(ct);
    }

    public async Task<CommunityComment?> FindTrackedByIdAsync(Guid commentId, CancellationToken ct = default)
    {
        return await _context.CommunityComments
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, ct);
    }

    public async Task AddAsync(CommunityComment comment, CancellationToken ct = default)
    {
        await _context.CommunityComments.AddAsync(comment, ct);
    }

    public void Update(CommunityComment comment)
    {
        _context.CommunityComments.Update(comment);
    }

    public async Task<int> SoftDeleteActiveByPostAsync(Guid postId, DateTime deletedAt, CancellationToken ct = default)
    {
        // One SQL statement: soft-delete every visible comment of the post so
        // a deleted post never leaves publicly visible comments behind.
        return await _context.CommunityComments
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsDeleted, true)
                .SetProperty(c => c.UpdatedAt, deletedAt), ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
