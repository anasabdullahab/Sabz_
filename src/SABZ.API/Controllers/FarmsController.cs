using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Farms;
using SABZ.Application.Interfaces;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

[ApiController]
[Route("api/farms")]
[Authorize]
public class FarmsController : ControllerBase
{
    private readonly IFarmService _farmService;

    public FarmsController(IFarmService farmService)
    {
        _farmService = farmService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFarm([FromBody] CreateFarmDto dto)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _farmService.CreateFarmAsync(userId, dto);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetFarms()
    {
        var userId = GetCurrentUserId();
        var farms = await _farmService.GetFarmsAsync(userId);
        return Ok(farms);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFarm(Guid id)
    {
        var userId = GetCurrentUserId();
        var farm = await _farmService.GetFarmByIdAsync(userId, id);
        return Ok(farm);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateFarm(Guid id, [FromBody] UpdateFarmDto dto)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _farmService.UpdateFarmAsync(userId, id, dto);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFarm(Guid id)
    {
        var userId = GetCurrentUserId();
        await _farmService.DeleteFarmAsync(userId, id);
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
                g => g.Select(r => r.ErrorMessage!).ToArray()
            );
    }
}
