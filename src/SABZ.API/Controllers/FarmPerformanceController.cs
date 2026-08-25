using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

/// <summary>
/// Farm Performance Dashboard &amp; Decision Intelligence (Prompt 11).
///
/// Read-only intelligence computed dynamically from existing SABZ data
/// (crops, Prompt 9 ledger, Prompt 7 monitoring checks) - never persisted,
/// never invented. This is NOT a loan, credit, banking, insurance,
/// investment, or financing system, and it uses no AI.
///
/// All endpoints require authentication; ownership is always derived from the
/// JWT user via user -> farm -> (crops, transactions, checks). UserId is
/// never accepted from the request.
/// </summary>
[ApiController]
[Authorize]
public class FarmPerformanceController : ControllerBase
{
    private readonly IFarmPerformanceService _farmPerformanceService;

    public FarmPerformanceController(IFarmPerformanceService farmPerformanceService)
    {
        _farmPerformanceService = farmPerformanceService;
    }

    /// <summary>
    /// Farm-level performance overview: crop counts, recorded financial
    /// totals, deterministic best/weakest recorded crop, factual overall
    /// status (NoRecordedData / LimitedRecordedData / RecordedActivityAvailable)
    /// and structured data limitations.
    /// Optional fromDate/toDate (UTC date-only; fromDate must be on or before
    /// toDate) filter only the financial ledger rows, never the crop records.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/performance")]
    public async Task<IActionResult> GetPerformance(
        Guid farmId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var summary = await _farmPerformanceService.GetPerformanceSummaryAsync(userId, farmId, fromDate, toDate, ct);
        return Ok(summary);
    }

    /// <summary>
    /// Per-crop recorded performance breakdown of the farm: totals, net
    /// result and the deterministic FinancialDataStatus of each crop
    /// (NoFinancialData / ExpensesOnly / IncomeOnly / RecordedIncomeAndExpenses).
    /// Optional fromDate/toDate with the same semantics as the overview.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/performance/crops")]
    public async Task<IActionResult> GetCropPerformance(
        Guid farmId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var breakdown = await _farmPerformanceService.GetCropPerformanceAsync(userId, farmId, fromDate, toDate, ct);
        return Ok(breakdown);
    }

    /// <summary>
    /// Recorded activity in SABZ over the farm's full history (never physical
    /// farm activity): financial transactions plus completed/skipped
    /// monitoring checks. No date-range parameters by design.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/performance/activity")]
    public async Task<IActionResult> GetActivity(Guid farmId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var activity = await _farmPerformanceService.GetActivitySummaryAsync(userId, farmId, ct);
        return Ok(activity);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new SABZ.Domain.Exceptions.AuthenticationException("Invalid token.");
        return userId;
    }
}
