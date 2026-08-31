namespace SABZ.Application.DTOs.CropPrice;

/// <summary>
/// Price information for one crop: latest recorded price plus the dated
/// historical records actually supplied by the provider. No synthetic
/// history, no interpolation - when nothing is available the response is an
/// honest "Unavailable" result with a limitation message.
/// </summary>
public class CropPriceDetailDto
{
    public string CropName { get; set; } = string.Empty;

    /// <summary>True when the requested name maps to a CropCatalog entry.</summary>
    public bool CropRecognized { get; set; }

    public CropPriceRecordDto? Latest { get; set; }
    public List<CropPriceRecordDto> HistoricalRecords { get; set; } = new();
    public DateTime? FirstDate { get; set; }
    public DateTime? LatestDate { get; set; }

    public string DataStatus { get; set; } = string.Empty;

    /// <summary>Honest limitation text when no price data exists.</summary>
    public string? Message { get; set; }

    public string Disclaimer { get; set; } = string.Empty;
}
