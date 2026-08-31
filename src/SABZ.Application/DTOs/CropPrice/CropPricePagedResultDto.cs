namespace SABZ.Application.DTOs.CropPrice;

/// <summary>
/// Paginated crop price feed (marketplace feed convention). The page is
/// computed over the provider's records in memory after deterministic
/// filtering - no N+1, no database writes.
/// </summary>
public class CropPricePagedResultDto
{
    public List<CropPriceRecordDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    /// <summary>Human-readable status of the underlying data source.</summary>
    public string DataStatus { get; set; } = string.Empty;

    public string Disclaimer { get; set; } = string.Empty;
}
