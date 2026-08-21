using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

/// <summary>
/// Farm Financial Health & Readiness Intelligence (Prompt 10).
///
/// Read-only analytics computed dynamically from the Prompt 9 financial
/// ledger - never persisted, never invented. This is NOT a loan, credit,
/// banking, insurance, investment, or financing system, and it uses no AI.
///
/// All endpoints require authentication; ownership is always derived from the
/// JWT user via user -> farm (-> crop) -> transactions. UserId is never
/// accepted from the request.
/// </summary>
[ApiController]
[Authorize]
public class FinancialHealthController : ControllerBase
{
    private readonly IFinancialHealthService _financialHealthService;

    public FinancialHealthController(IFinancialHealthService financialHealthService)
    {
        _financialHealthService = financialHealthService;
    }

    /// <summary>
    /// Deterministic financial health summary for a farm: totals, counts,
    /// date bounds, active days and the health indicator
    /// (NoData / LimitedData / LossRecorded / BreakEven / PositiveNetResult).
    /// Optional fromDate/toDate (UTC date-only; fromDate must be on or before toDate).
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/financial-health")]
    public async Task<IActionResult> GetFarmHealth(
        Guid farmId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var summary = await _financialHealthService.GetFarmHealthAsync(userId, farmId, fromDate, toDate, ct);
        return Ok(summary);
    }

    /// <summary>
    /// Income and expense category breakdowns with dynamically computed
    /// percentages (never persisted). Optional fromDate/toDate.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/financial-health/categories")]
    public async Task<IActionResult> GetCategoryBreakdown(
        Guid farmId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var breakdown = await _financialHealthService.GetCategoryBreakdownAsync(userId, farmId, fromDate, toDate, ct);
        return Ok(breakdown);
    }

    /// <summary>
    /// Monthly (yyyy-MM) financial activity for a farm, grouped in SQL.
    /// Optional fromDate/toDate.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/financial-health/activity")]
    public async Task<IActionResult> GetActivity(
        Guid farmId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var activity = await _financialHealthService.GetActivityAsync(userId, farmId, fromDate, toDate, ct);
        return Ok(activity);
    }

    /// <summary>
    /// Financial record completeness (data readiness) over the farm's FULL
    /// history - five deterministic checks worth 20 points each (0-100).
    /// This measures recorded data completeness only; it is not a credit or
    /// loan score. No date-range parameters by design.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/financial-health/completeness")]
    public async Task<IActionResult> GetCompleteness(Guid farmId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var completeness = await _financialHealthService.GetCompletenessAsync(userId, farmId, ct);
        return Ok(completeness);
    }

    /// <summary>
    /// Financial health summary scoped to one crop of the user's farm.
    /// The crop must belong to the farm (otherwise 400). Optional fromDate/toDate.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/crops/{cropId:guid}/financial-health")]
    public async Task<IActionResult> GetCropHealth(
        Guid farmId,
        Guid cropId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var summary = await _financialHealthService.GetCropHealthAsync(userId, farmId, cropId, fromDate, toDate, ct);
        return Ok(summary);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new SABZ.Domain.Exceptions.AuthenticationException("Invalid token.");
        return userId;
    }
}
