using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.MarketplaceInbox;
using SABZ.Application.Interfaces;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

/// <summary>
/// Private farmer-to-farmer inbox for marketplace listings (Prompt 15).
/// Only the two conversation participants (buyer and seller) may read or
/// send messages; membership is verified against the JWT identity on every
/// request, and no request body ever accepts a sender/buyer/seller id.
///
/// There is no public message feed, and responses never contain user ids,
/// emails or phone numbers - display names only. This feature produces no
/// notifications and no financial records.
/// </summary>
[ApiController]
[Authorize]
public class MarketplaceInboxController : ControllerBase
{
    private readonly IMarketplaceInboxService _inboxService;

    public MarketplaceInboxController(IMarketplaceInboxService inboxService)
    {
        _inboxService = inboxService;
    }

    /// <summary>The authenticated farmer's conversations, newest activity first, DB-side paginated (page=1, pageSize=20, max 50).</summary>
    [HttpGet("api/marketplace/inbox")]
    public async Task<IActionResult> GetInbox(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _inboxService.GetInboxAsync(userId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// One conversation with listing context and DB-side paginated messages
    /// (participants only; non-participants receive 403).
    /// </summary>
    [HttpGet("api/marketplace/inbox/{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _inboxService.GetConversationAsync(userId, conversationId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// "Message Seller": starts (or reuses) the private conversation for the
    /// listing and sends the first message. Sellers cannot contact their own
    /// listing; deleted/unknown listings return 404.
    /// </summary>
    [HttpPost("api/marketplace/listings/{listingId:guid}/contact")]
    public async Task<IActionResult> ContactSeller(Guid listingId, [FromBody] StartMarketplaceConversationDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _inboxService.ContactSellerAsync(userId, listingId, dto, ct);
        return Ok(result);
    }

    /// <summary>Send a message into an existing conversation (participants only; sender comes from the JWT).</summary>
    [HttpPost("api/marketplace/inbox/{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid conversationId, [FromBody] SendMarketplaceMessageDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _inboxService.SendMessageAsync(userId, conversationId, dto, ct);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim, out var userId))
            throw new SABZ.Domain.Exceptions.AuthenticationException("Invalid token.");
        return userId;
    }

    private static Dictionary<string, string[]> ValidateModel(object model)
    {
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        if (results.Count == 0)
            return new Dictionary<string, string[]>();

        return results
            .Where(r => r != ValidationResult.Success)
            .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "Error")
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.ErrorMessage ?? "Invalid value.").ToArray());
    }
}
