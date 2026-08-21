using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Monitoring;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

/// <summary>
/// Smart crop monitoring schedule (Prompt 7).
/// All endpoints require authentication; ownership is always derived from the
/// JWT user via user -> farm -> crop -> monitoring check. userId is never
/// accepted from the request.
/// </summary>
[ApiController]
[Authorize]
public class MonitoringController : ControllerBase
{
    private readonly IMonitoringService _monitoringService;

    public MonitoringController(IMonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }

    /// <summary>All monitoring checks of one crop (ownership verified).</summary>
    [HttpGet("api/crops/{cropId:guid}/monitoring")]
    public async Task<IActionResult> GetChecksForCrop(Guid cropId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var checks = await _monitoringService.GetChecksForCropAsync(userId, cropId, ct);
        return Ok(checks);
    }

    /// <summary>
    /// Idempotent monitoring-check generation for a crop (safe for crops created
    /// before Prompt 7, crops without a planting date and crops with no rules).
    /// </summary>
    [HttpPost("api/crops/{cropId:guid}/monitoring/generate")]
    public async Task<IActionResult> GenerateChecks(Guid cropId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _monitoringService.EnsureChecksForCropAsync(userId, cropId, ct);
        return Ok(result);
    }

    /// <summary>
    /// The authenticated user's due checks (scheduled date reached, not completed
    /// or skipped), most overdue first.
    /// </summary>
    [HttpGet("api/monitoring/due")]
    public async Task<IActionResult> GetDueChecks(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var checks = await _monitoringService.GetDueChecksAsync(userId, ct);
        return Ok(checks);
    }

    /// <summary>The authenticated user's upcoming checks (future scheduled date), soonest first.</summary>
    [HttpGet("api/monitoring/upcoming")]
    public async Task<IActionResult> GetUpcomingChecks(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var checks = await _monitoringService.GetUpcomingChecksAsync(userId, ct);
        return Ok(checks);
    }

    /// <summary>
    /// Complete a monitoring check with a controlled observation
    /// ("Normal" or "SomethingSuspicious"). A suspicious observation recommends
    /// (never triggers) the existing Prompt 6 photo analysis workflow.
    /// </summary>
    [HttpPost("api/monitoring/{checkId:guid}/complete")]
    public async Task<IActionResult> CompleteCheck(Guid checkId, [FromBody] CompleteMonitoringCheckRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _monitoringService.CompleteCheckAsync(userId, checkId, request, ct);
        return Ok(result);
    }

    /// <summary>Skip a monitoring check. A skipped check never appears as due afterwards.</summary>
    [HttpPost("api/monitoring/{checkId:guid}/skip")]
    public async Task<IActionResult> SkipCheck(Guid checkId, [FromBody] SkipMonitoringCheckRequestDto? request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _monitoringService.SkipCheckAsync(userId, checkId, request, ct);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new SABZ.Domain.Exceptions.AuthenticationException("Invalid token.");
        return userId;
    }
}
