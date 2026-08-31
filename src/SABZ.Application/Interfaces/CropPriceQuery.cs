namespace SABZ.Application.Interfaces;

/// <summary>
/// Parsed, validated query for the crop price feed. The controller builds
/// this from the query string; the service applies it deterministically.
/// Only filters the provider layer can honour are exposed - nothing invented.
/// </summary>
public class CropPriceQuery
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public string? Crop { get; set; }
    public string? Province { get; set; }
    public string? District { get; set; }
    public string? Market { get; set; }

    /// <summary>Inclusive lower bound (date part only).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Inclusive upper bound (date part only).</summary>
    public DateTime? ToDate { get; set; }

    public int Page { get; set; } = DefaultPage;
    public int PageSize { get; set; } = DefaultPageSize;
}
