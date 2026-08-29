namespace SABZ.Application.DTOs.Community;

/// <summary>
/// Small reusable DB-side pagination envelope (Prompt 14). No shared
/// pagination model existed before the community feature, so this lives with
/// the community DTOs but is reusable by later features.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
