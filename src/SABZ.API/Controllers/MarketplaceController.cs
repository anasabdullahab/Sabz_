using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Marketplace;
using SABZ.Application.Interfaces;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

/// <summary>
/// Farmer marketplace (Prompt 15): agricultural equipment listings for sale
/// or rent. A CONNECTION/DISCOVERY system only - SABZ never processes
/// payments, orders, escrow or any financial transaction; the flow is
/// Discover listing -> View details -> Contact/message farmer -> Arrange
/// privately outside SABZ.
///
/// All endpoints require authentication (reads included, consistent with the
/// rest of SABZ). Ownership always comes from the JWT user; no endpoint ever
/// accepts a client-supplied user id, and the public feed never exposes the
/// seller's contact number.
/// </summary>
[ApiController]
[Authorize]
public class MarketplaceController : ControllerBase
{
    private readonly IMarketplaceService _marketplaceService;

    public MarketplaceController(IMarketplaceService marketplaceService)
    {
        _marketplaceService = marketplaceService;
    }

    /// <summary>
    /// Marketplace feed, newest first, DB-side paginated (page=1, pageSize=20,
    /// max 50) with optional search/category/listingType/location/condition
    /// filters. Never includes seller contact numbers.
    /// </summary>
    [HttpGet("api/marketplace/listings")]
    public async Task<IActionResult> GetListings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? listingType = null,
        [FromQuery] string? location = null,
        [FromQuery] string? condition = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _marketplaceService.GetListingsAsync(
            userId, page, pageSize, search, category, listingType, location, condition, ct);
        return Ok(result);
    }

    /// <summary>Create a listing owned by the authenticated farmer (no client-supplied owner).</summary>
    [HttpPost("api/marketplace/listings")]
    public async Task<IActionResult> CreateListing([FromBody] CreateMarketplaceListingDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _marketplaceService.CreateListingAsync(userId, dto, ct);
        return Ok(result);
    }

    /// <summary>Listing detail; the contact number is returned to the owner only.</summary>
    [HttpGet("api/marketplace/listings/{listingId:guid}")]
    public async Task<IActionResult> GetListing(Guid listingId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _marketplaceService.GetListingAsync(userId, listingId, ct);
        return Ok(result);
    }

    /// <summary>Full update of an owned listing (owner only, 403 otherwise; ownership cannot change).</summary>
    [HttpPut("api/marketplace/listings/{listingId:guid}")]
    public async Task<IActionResult> UpdateListing(Guid listingId, [FromBody] UpdateMarketplaceListingDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _marketplaceService.UpdateListingAsync(userId, listingId, dto, ct);
        return Ok(result);
    }

    /// <summary>Soft-delete an owned listing; private inbox history stays intact.</summary>
    [HttpDelete("api/marketplace/listings/{listingId:guid}")]
    public async Task<IActionResult> DeleteListing(Guid listingId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _marketplaceService.DeleteListingAsync(userId, listingId, ct);
        return NoContent();
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
