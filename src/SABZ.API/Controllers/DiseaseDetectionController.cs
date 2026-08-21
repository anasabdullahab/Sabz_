using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.Interfaces;
using SABZ.Domain.Exceptions;

namespace SABZ.API.Controllers;

/// <summary>
/// AI crop disease identification (Prompt 6).
/// Farmers upload a crop/leaf photograph and receive a cautious AI assessment
/// plus agricultural guidance. The authenticated user comes from the JWT only;
/// userId/ownerId are never accepted from the request body.
/// </summary>
[ApiController]
[Route("api/farms/{farmId:guid}/disease-detection")]
[Authorize]
[Consumes("multipart/form-data")]
public class DiseaseDetectionController : ControllerBase
{
    private readonly IDiseaseDetectionService _diseaseDetectionService;

    public DiseaseDetectionController(IDiseaseDetectionService diseaseDetectionService)
    {
        _diseaseDetectionService = diseaseDetectionService;
    }

    /// <summary>
    /// Analyse an uploaded crop photograph for disease.
    /// multipart/form-data fields: image (file, required), cropId (guid, optional),
    /// notes (string, optional). The farm must belong to the authenticated user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DetectDisease(
        Guid farmId,
        IFormFile image,
        [FromForm] Guid? cropId,
        [FromForm] string? notes,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (image is null || image.Length == 0)
            throw new ValidationException("An image file is required. Please upload a photograph of the crop leaf or plant.");

        await using var stream = new MemoryStream();
        await image.CopyToAsync(stream, ct);

        var result = await _diseaseDetectionService.DetectAsync(
            userId,
            farmId,
            stream.ToArray(),
            image.ContentType,
            image.FileName,
            cropId,
            notes,
            ct);

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
