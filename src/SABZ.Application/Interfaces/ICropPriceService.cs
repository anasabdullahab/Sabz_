using SABZ.Application.DTOs.CropPrice;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Crop price intelligence (Prompt 17) - informational only. Deterministic
/// service that filters and paginates provider records and always attaches
/// source/date/status context plus the mandatory disclaimer. It never
/// predicts prices, recommends buying/selling, writes to the database, or
/// touches the financial ledger.
/// </summary>
public interface ICropPriceService
{
    /// <summary>Paginated, filterable price feed.</summary>
    Task<CropPricePagedResultDto> GetPricesAsync(CropPriceQuery query, CancellationToken ct = default);

    /// <summary>Price detail (latest + supplied history) for a single crop.</summary>
    Task<CropPriceDetailDto> GetPriceByCropAsync(string cropName, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
}
