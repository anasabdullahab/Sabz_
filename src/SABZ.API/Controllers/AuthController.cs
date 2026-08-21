using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Auth;
using SABZ.Application.Interfaces;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Run DataAnnotations validation
        var validationErrors = ValidateModel(request);
        if (validationErrors.Count > 0)
            throw new DomainValidationException(validationErrors);

        var result = await _authService.RegisterAsync(request);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var validationErrors = ValidateModel(request);
        if (validationErrors.Count > 0)
            throw new DomainValidationException(validationErrors);

        var result = await _authService.LoginAsync(request);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token." });

        var user = await _authService.GetCurrentUserAsync(userId);
        return Ok(user);
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
                g => g.Select(r => r.ErrorMessage!).ToArray()
            );
    }
}
