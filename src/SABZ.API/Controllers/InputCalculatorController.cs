using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.FertilizerCalculator;
using SABZ.Application.DTOs.InputCalculator;
using SABZ.Application.Interfaces;
using SABZ.Application.Services.CropKnowledge;
using SABZ.Application.Services.FertilizerCalculator;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

/// <summary>
/// Precision crop input &amp; dosage calculator (Prompt 16). Exactly one
/// endpoint: deterministic farm-area × dosage-rate arithmetic for one of the
/// caller's own farms. Pure calculation on demand - no persistence, no AI,
/// no background jobs, no financial/marketplace interaction.
///
/// Ownership comes from the JWT only; the client sends a farmId and never a
/// userId or a farm area. No try/catch here: domain exceptions are mapped by
/// the <c>GlobalExceptionMiddleware</c> (400/401/403/404).
/// </summary>
[ApiController]
[Authorize]
public class InputCalculatorController : ControllerBase
{
    private readonly IInputCalculatorService _inputCalculatorService;
    private readonly FertilizerCalculatorService _fertilizerCalculatorService;

    public InputCalculatorController(IInputCalculatorService inputCalculatorService, FertilizerCalculatorService fertilizerCalculatorService)
    {
        _inputCalculatorService = inputCalculatorService;
        _fertilizerCalculatorService = fertilizerCalculatorService;
    }

    /// <summary>
    /// Calculates the required quantity of an agricultural input:
    /// farm area (from the Farm record) × supplied dosage rate. The rate is
    /// used exactly as supplied - SABZ never invents or prescribes rates.
    /// </summary>
    [HttpPost("api/farms/{farmId:guid}/input-calculator")]
    public async Task<IActionResult> Calculate(Guid farmId, [FromBody] InputCalculatorRequestDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _inputCalculatorService.CalculateAsync(userId, farmId, dto, ct);
        return Ok(result);
    }

    /// <summary>
    /// Automated fertilizer presets: crop name + farm size (acres) in, exact
    /// bag counts and application schedule out. Powered by the local crop
    /// knowledge base - no AI, no external calls.
    /// </summary>
    [HttpPost("api/fertilizer-calculator")]
    public IActionResult CalculateFertilizer([FromBody] FertilizerCalculatorRequestDto dto)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        return Ok(_fertilizerCalculatorService.Calculate(dto));
    }

    /// <summary>
    /// Returns the local crop knowledge base catalogue (names, seasons,
    /// maturity days, stage timeline, soil suitability) for searchable crop
    /// dropdowns, harvest-window estimation and stage progress bars.
    /// </summary>
    [HttpGet("api/crop-knowledge")]
    [AllowAnonymous]
    public IActionResult GetCropKnowledge()
    {
        var crops = CropKnowledgeBase.Entries.Select(e => new
        {
            e.Name,
            e.NameUrdu,
            e.Category,
            e.Season,
            e.MaturityDays,
            e.SuitableSoil,
            e.NitrogenImpact,
            e.WaterRequirement,
            StageTimeline = new
            {
                Germination = new[] { e.StageTimeline.Germination.StartDay, e.StageTimeline.Germination.EndDay },
                Vegetative = new[] { e.StageTimeline.Vegetative.StartDay, e.StageTimeline.Vegetative.EndDay },
                Flowering = new[] { e.StageTimeline.Flowering.StartDay, e.StageTimeline.Flowering.EndDay },
                Maturity = new[] { e.StageTimeline.Maturity.StartDay, e.StageTimeline.Maturity.EndDay },
            },
        });
        return Ok(crops);
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
