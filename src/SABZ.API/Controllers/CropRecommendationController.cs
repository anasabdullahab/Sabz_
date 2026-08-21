using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

/// <summary>
/// Next-crop recommendation (Prompt 5).
/// Reuses the Prompt 4 suitability evaluation and adds crop-history / crop-change guidance.
/// The Prompt 4 endpoint GET /api/farms/{farmId}/crop-suitability remains unchanged.
/// </summary>
[ApiController]
[Route("api/farms/{farmId:guid}/crop-recommendations")]
[Authorize]
public class CropRecommendationController : ControllerBase
{
    private readonly ICropRecommendationService _recommendationService;

    public CropRecommendationController(ICropRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    /// <summary>
    /// Get next-crop recommendations for an owned farm.
    /// Season is optional ("Rabi"/"Kharif"); auto-detected from the current month when omitted.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCropRecommendations(Guid farmId, [FromQuery] string? season, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _recommendationService.RecommendAsync(userId, farmId, season, ct);
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
