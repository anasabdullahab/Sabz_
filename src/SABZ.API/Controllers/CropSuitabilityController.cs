using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;

namespace SABZ.API.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/crop-suitability")]
[Authorize]
public class CropSuitabilityController : ControllerBase
{
    private readonly ICropSuitabilityService _cropSuitabilityService;

    public CropSuitabilityController(ICropSuitabilityService cropSuitabilityService)
    {
        _cropSuitabilityService = cropSuitabilityService;
    }

    /// <summary>
    /// Evaluate crop suitability for a farm based on its location, soil,
    /// irrigation and current weather forecast.
    /// Requires authentication; the authenticated user must own the farm.
    /// Optional season query parameter: 'Rabi' or 'Kharif' (auto-detected when omitted).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCropSuitability(Guid farmId, [FromQuery] string? season, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _cropSuitabilityService.EvaluateAsync(userId, farmId, season, ct);
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
