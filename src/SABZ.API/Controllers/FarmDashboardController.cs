using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

/// <summary>
/// Unified Farm Dashboard &amp; Insights (Prompt 12).
///
/// A single read-only, farm-level overview that aggregates EXISTING SABZ
/// data and calculations - farm details, crops, Prompt 7 monitoring, Prompt 8
/// notifications, the Prompt 9 ledger, Prompt 10 financial health, Prompt 11
/// performance and (when available) external Prompt 3 weather. It is a pure
/// aggregation/orchestration layer, never a new source of truth: nothing
/// derived is persisted, no business logic is duplicated, and it uses no AI.
///
/// All endpoints require authentication; ownership is always derived from the
/// JWT user via user -> farm. UserId is never accepted from the request.
/// </summary>
[ApiController]
[Authorize]
public class FarmDashboardController : ControllerBase
{
    private readonly IFarmDashboardService _farmDashboardService;

    public FarmDashboardController(IFarmDashboardService farmDashboardService)
    {
        _farmDashboardService = farmDashboardService;
    }

    /// <summary>
    /// The unified dashboard for one farm of the authenticated user: farm
    /// facts, crop summaries, monitoring counts, unread + recent
    /// notifications, the Prompt 9 financial summary, Prompt 10 health and
    /// completeness, Prompt 11 performance, optional external weather,
    /// structured limitations and a factual disclaimer. Computed entirely at
    /// request time; nothing is persisted. Returns 404 for an unknown farm
    /// and 403 for a farm owned by another user.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/dashboard")]
    public async Task<IActionResult> GetDashboard(Guid farmId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var dashboard = await _farmDashboardService.GetDashboardAsync(userId, farmId, ct);
        return Ok(dashboard);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new SABZ.Domain.Exceptions.AuthenticationException("Invalid token.");
        return userId;
    }
}
