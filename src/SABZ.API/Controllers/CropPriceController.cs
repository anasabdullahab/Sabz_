using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

/// <summary>
/// Crop price intelligence (Prompt 17): factual agricultural commodity price
/// information for supported crops/markets. INFORMATIONAL ONLY - SABZ never
/// predicts prices, recommends buying/selling, guarantees profit, gives
/// investment advice, or creates orders/payments/financial transactions.
///
/// All endpoints require authentication and are strictly read-only: no
/// endpoint accepts a user id, exposes a user id, requires a farm, or modifies
/// database state. Every price carries its source, price date, data status and
/// the mandatory disclaimer. Provider failures are mapped to HTTP 502 by the
/// <c>GlobalExceptionMiddleware</c> and never leak internal details.
/// </summary>
[ApiController]
[Authorize]
public class CropPriceController : ControllerBase
{
    private readonly ICropPriceService _cropPriceService;

    public CropPriceController(ICropPriceService cropPriceService)
    {
        _cropPriceService = cropPriceService;
    }

    /// <summary>
    /// Paginated/filterable crop price feed (page=1, pageSize=20, max 50).
    /// Supported filters: crop, province, district, market, fromDate, toDate
    /// (both dates inclusive). Only filters the provider layer can honour are
    /// exposed.
    /// </summary>
    [HttpGet("api/crop-prices")]
    public async Task<IActionResult> GetPrices(
        [FromQuery] string? crop = null,
        [FromQuery] string? province = null,
        [FromQuery] string? district = null,
        [FromQuery] string? market = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] int page = CropPriceQuery.DefaultPage,
        [FromQuery] int pageSize = CropPriceQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var query = new CropPriceQuery
        {
            Crop = crop,
            Province = province,
            District = district,
            Market = market,
            FromDate = ParseDate(fromDate, "fromDate"),
            ToDate = ParseDate(toDate, "toDate"),
            Page = page,
            PageSize = pageSize
        };

        var result = await _cropPriceService.GetPricesAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Price information for a single crop: latest recorded price plus the
    /// dated historical records the provider actually supplies. Optional
    /// fromDate/toDate filters are inclusive. Unknown crop -> 404; recognised
    /// crop with no data -> honest "Unavailable" result (never a placeholder).
    /// </summary>
    [HttpGet("api/crop-prices/{cropName}")]
    public async Task<IActionResult> GetPriceByCrop(
        string cropName,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        CancellationToken ct = default)
    {
        var from = ParseDate(fromDate, "fromDate");
        var to = ParseDate(toDate, "toDate");

        var result = await _cropPriceService.GetPriceByCropAsync(cropName, from, to, ct);
        return Ok(result);
    }

    /// <summary>
    /// Parses an optional yyyy-MM-dd filter value; a supplied-but-unparseable
    /// date is a client error (400), not a provider failure.
    /// </summary>
    private static DateTime? ParseDate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        throw new DomainValidationException($"{fieldName} is not a valid date (expected yyyy-MM-dd).");
    }
}
