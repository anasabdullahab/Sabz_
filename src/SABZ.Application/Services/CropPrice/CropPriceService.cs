using SABZ.Application.DTOs.CropPrice;
using SABZ.Application.Interfaces;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.CropPrice;

/// <summary>
/// Crop price intelligence (Prompt 17) - informational only.
///
/// Deterministic filtering/pagination over the provider's factual records.
/// Every returned record carries source + price date + data status and the
/// mandatory disclaimer. The service never predicts prices, recommends
/// buying or selling, writes anything to the database, or interacts with the
/// financial ledger, marketplace or notifications.
/// </summary>
public class CropPriceService : ICropPriceService
{
    /// <summary>Mandatory disclaimer, returned verbatim on every response.</summary>
    public const string Disclaimer =
        "Crop prices shown by SABZ are informational market data. Prices may change " +
        "and SABZ does not predict prices, guarantee future prices, or provide " +
        "financial, investment, or trading advice.";

    private readonly ICropPriceProvider _provider;
    private readonly ICropSuitabilityDataRepository _suitabilityData;

    public CropPriceService(ICropPriceProvider provider, ICropSuitabilityDataRepository suitabilityData)
    {
        _provider = provider;
        _suitabilityData = suitabilityData;
    }

    public async Task<CropPricePagedResultDto> GetPricesAsync(CropPriceQuery query, CancellationToken ct = default)
    {
        ValidatePaging(query.Page, query.PageSize);
        ValidateDateRange(query.FromDate, query.ToDate);

        var records = await _provider.GetRecordsAsync(ct);
        foreach (var record in records)
            record.Disclaimer = Disclaimer;

        // Crop filter resolves through the existing CropCatalog (case-insensitive,
        // safely normalised) - never a second catalog. An unresolvable crop name is
        // an honest empty result, never a fabricated one.
        var cropFilterSupplied = !string.IsNullOrWhiteSpace(query.Crop);
        string? canonicalCrop = null;
        if (cropFilterSupplied)
        {
            var catalog = await _suitabilityData.GetCatalogAsync(ct);
            canonicalCrop = ResolveCatalogName(query.Crop!, catalog.Select(c => c.Name));
        }

        var filtered = records
            .Where(r => !cropFilterSupplied || string.Equals(r.CropName, canonicalCrop, StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrWhiteSpace(query.Province) || string.Equals(r.Province.Trim(), query.Province.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrWhiteSpace(query.District) || string.Equals(r.District.Trim(), query.District.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrWhiteSpace(query.Market) || r.Market.Contains(query.Market.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(r => query.FromDate is null || r.PriceDate.Date >= query.FromDate.Value.Date)
            .Where(r => query.ToDate is null || r.PriceDate.Date <= query.ToDate.Value.Date)
            .OrderBy(r => r.CropName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(r => r.PriceDate)
            .ThenBy(r => r.District, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Market, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = filtered.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var items = filtered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new CropPricePagedResultDto
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            DataStatus = totalCount > 0 ? ProviderStatus() : CropPriceDataStatuses.Unavailable,
            Disclaimer = Disclaimer
        };
    }

    public async Task<CropPriceDetailDto> GetPriceByCropAsync(string cropName, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var trimmed = cropName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ValidationException("Crop name is required.");
        ValidateDateRange(fromDate, toDate);

        var catalog = await _suitabilityData.GetCatalogAsync(ct);
        var canonical = ResolveCatalogName(trimmed, catalog.Select(c => c.Name))
            ?? throw new NotFoundException("Crop not found.");

        var records = (await _provider.GetRecordsAsync(ct))
            .Where(r => string.Equals(r.CropName, canonical, StringComparison.OrdinalIgnoreCase))
            .Where(r => fromDate is null || r.PriceDate.Date >= fromDate.Value.Date)
            .Where(r => toDate is null || r.PriceDate.Date <= toDate.Value.Date)
            .OrderByDescending(r => r.PriceDate)
            .ThenBy(r => r.District, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Market, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var record in records)
            record.Disclaimer = Disclaimer;

        if (records.Count == 0)
        {
            // Honest limitation - never a placeholder or demo price.
            return new CropPriceDetailDto
            {
                CropName = canonical,
                CropRecognized = true,
                DataStatus = CropPriceDataStatuses.Unavailable,
                Message = $"No price data is currently available for {canonical}.",
                Disclaimer = Disclaimer
            };
        }

        return new CropPriceDetailDto
        {
            CropName = canonical,
            CropRecognized = true,
            Latest = records[0],
            HistoricalRecords = records,
            FirstDate = records.Min(r => r.PriceDate),
            LatestDate = records.Max(r => r.PriceDate),
            DataStatus = ProviderStatus(),
            Disclaimer = Disclaimer
        };
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Data status reported for the feed/detail as a whole. "Live" is only
    /// ever used when the provider genuinely returned current data.
    /// </summary>
    private string ProviderStatus()
        => _provider.IsLive ? CropPriceDataStatuses.Live : CropPriceDataStatuses.Reference;

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1)
            throw new ValidationException("Page must be at least 1.");
        if (pageSize < 1)
            throw new ValidationException("Page size must be at least 1.");
        if (pageSize > CropPriceQuery.MaxPageSize)
            throw new ValidationException($"Page size must not exceed {CropPriceQuery.MaxPageSize}.");
    }

    private static void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        if (fromDate is not null && toDate is not null && fromDate.Value.Date > toDate.Value.Date)
            throw new ValidationException("fromDate must be on or before toDate.");
    }

    /// <summary>
    /// Safe, deterministic name matching: compares lower-cased names with all
    /// non-alphanumeric characters removed, so "Gram (Chickpea)" matches
    /// "gram chickpea" and "chili pepper" matches "Chili Pepper". Returns the
    /// canonical catalog name, or null when nothing matches.
    /// </summary>
    private static string? ResolveCatalogName(string query, IEnumerable<string> catalogNames)
    {
        var queryKey = NormalizeKey(query);
        if (queryKey.Length == 0)
            return null;

        foreach (var name in catalogNames)
        {
            if (NormalizeKey(name) == queryKey)
                return name;
        }
        return null;
    }

    private static string NormalizeKey(string value)
        => new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
