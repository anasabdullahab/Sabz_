using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Crops;
using SABZ.Application.Interfaces;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

[ApiController]
[Authorize]
public class CropsController : ControllerBase
{
    private readonly ICropService _cropService;

    public CropsController(ICropService cropService)
    {
        _cropService = cropService;
    }

    [HttpPost("api/farms/{farmId:guid}/crops")]
    public async Task<IActionResult> CreateCrop(Guid farmId, [FromBody] CreateCropDto dto)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _cropService.CreateCropAsync(userId, farmId, dto);
        return Ok(result);
    }

    [HttpGet("api/farms/{farmId:guid}/crops")]
    public async Task<IActionResult> GetCropsByFarm(Guid farmId)
    {
        var userId = GetCurrentUserId();
        var crops = await _cropService.GetCropsByFarmAsync(userId, farmId);
        return Ok(crops);
    }

    [HttpGet("api/crops/{id:guid}")]
    public async Task<IActionResult> GetCrop(Guid id)
    {
        var userId = GetCurrentUserId();
        var crop = await _cropService.GetCropByIdAsync(userId, id);
        return Ok(crop);
    }

    [HttpPut("api/crops/{id:guid}")]
    public async Task<IActionResult> UpdateCrop(Guid id, [FromBody] UpdateCropDto dto)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _cropService.UpdateCropAsync(userId, id, dto);
        return Ok(result);
    }

    [HttpDelete("api/crops/{id:guid}")]
    public async Task<IActionResult> DeleteCrop(Guid id)
    {
        var userId = GetCurrentUserId();
        await _cropService.DeleteCropAsync(userId, id);
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
