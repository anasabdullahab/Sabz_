namespace SABZ.Application.DTOs.CropPrice;

/// <summary>
/// One factual commodity price record. Every record always carries its
/// source, price date and data status - a price is never returned without
/// its source/date context. Prices are decimal; the source unit is preserved
/// exactly (no blind unit conversion).
/// </summary>
public class CropPriceRecordDto
{
    public string CropName { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime PriceDate { get; set; }

    /// <summary>Where the price came from, e.g. "SABZ Reference Dataset".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Live / Historical / Reference / Unavailable - never inflated.</summary>
    public string DataStatus { get; set; } = string.Empty;

    public string Disclaimer { get; set; } = string.Empty;
}
