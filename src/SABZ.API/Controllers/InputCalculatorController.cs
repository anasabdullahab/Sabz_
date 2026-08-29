using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.InputCalculator;
using SABZ.Application.Interfaces;
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

    public InputCalculatorController(IInputCalculatorService inputCalculatorService)
    {
        _inputCalculatorService = inputCalculatorService;
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
