using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Agronomist;
using SABZ.Application.Interfaces;
using SABZ.Domain.Exceptions;

namespace SABZ.API.Controllers;

/// <summary>
/// Voice-first AI agronomist assistant (Prompt 13). Farmers ask agriculture
/// questions by text or voice and receive an informational AI answer with
/// focused farm context. The assistant is strictly read-only. The authenticated
/// user comes from the JWT only; userId/ownerId are never accepted from the
/// request. Provider HTTP logic lives in Infrastructure, never here.
/// </summary>
[ApiController]
[Route("api/farms/{farmId:guid}/agronomist")]
[Authorize]
public class AgronomistAssistantController : ControllerBase
{
    private readonly IAgronomistAssistantService _assistantService;

    public AgronomistAssistantController(IAgronomistAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    /// <summary>
    /// Ask an agriculture question as text. Body: { "message": "..." }.
    /// The farm must belong to the authenticated user.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(Guid farmId, TextAgronomistQuestionDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _assistantService.ChatAsync(userId, farmId, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Ask an agriculture question by voice. multipart/form-data field: audio
    /// (file, required). The audio is transcribed in memory (never stored) and
    /// answered. The farm must belong to the authenticated user.
    /// </summary>
    [HttpPost("voice")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Voice(Guid farmId, IFormFile audio, CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (audio is null || audio.Length == 0)
            throw new ValidationException("An audio file is required. Please record and upload your voice question.");

        await using var stream = new MemoryStream();
        await audio.CopyToAsync(stream, ct);

        var result = await _assistantService.VoiceAsync(userId, farmId, stream.ToArray(), audio.ContentType, audio.FileName, ct);
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
