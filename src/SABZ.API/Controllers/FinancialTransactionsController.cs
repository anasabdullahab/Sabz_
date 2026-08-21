using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SABZ.Application.DTOs.Financial;
using SABZ.Application.Interfaces;
using DomainValidationException = SABZ.Domain.Exceptions.ValidationException;

namespace SABZ.API.Controllers;

/// <summary>
/// Farm profit & loss financial ledger (Prompt 9). Farmer-entered income and
/// expense records with dynamically computed P&L summaries.
///
/// All endpoints require authentication; ownership is always derived from the
/// JWT user via user -> farm -> transaction. UserId/OwnerId is never accepted
/// from the request, and the system never invents financial data.
/// </summary>
[ApiController]
[Authorize]
public class FinancialTransactionsController : ControllerBase
{
    private readonly IFinancialService _financialService;

    public FinancialTransactionsController(IFinancialService financialService)
    {
        _financialService = financialService;
    }

    /// <summary>Create a financial transaction on one of the user's farms.</summary>
    [HttpPost("api/farms/{farmId:guid}/transactions")]
    public async Task<IActionResult> CreateTransaction(Guid farmId, [FromBody] CreateFinancialTransactionDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _financialService.CreateTransactionAsync(userId, farmId, dto, ct);
        return Ok(result);
    }

    /// <summary>
    /// The user's transactions for a farm, newest first, capped at <c>take</c>
    /// (default 50, maximum 100). Optional filters: type, category, cropId,
    /// fromDate, toDate.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(
        Guid farmId,
        [FromQuery] string? type,
        [FromQuery] string? category,
        [FromQuery] Guid? cropId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? take,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var transactions = await _financialService.GetTransactionsAsync(
            userId, farmId, type, category, cropId, fromDate, toDate, take, ct);
        return Ok(transactions);
    }

    /// <summary>Get one of the user's transactions by id (ownership verified).</summary>
    [HttpGet("api/transactions/{id:guid}")]
    public async Task<IActionResult> GetTransaction(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var transaction = await _financialService.GetTransactionByIdAsync(userId, id, ct);
        return Ok(transaction);
    }

    /// <summary>
    /// Full update of one of the user's transactions; every field is
    /// re-validated and farm/crop ownership is re-checked.
    /// </summary>
    [HttpPut("api/transactions/{id:guid}")]
    public async Task<IActionResult> UpdateTransaction(Guid id, [FromBody] UpdateFinancialTransactionDto dto, CancellationToken ct)
    {
        var errors = ValidateModel(dto);
        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        var userId = GetCurrentUserId();
        var result = await _financialService.UpdateTransactionAsync(userId, id, dto, ct);
        return Ok(result);
    }

    /// <summary>Delete one of the user's transactions.</summary>
    [HttpDelete("api/transactions/{id:guid}")]
    public async Task<IActionResult> DeleteTransaction(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _financialService.DeleteTransactionAsync(userId, id, ct);
        return NoContent();
    }

    /// <summary>
    /// Dynamically computed P&L summary for a farm (optional cropId and date
    /// range): totalIncome, totalExpenses, netProfitLoss, transactionCount.
    /// Nothing is persisted - totals always come from raw transactions.
    /// </summary>
    [HttpGet("api/farms/{farmId:guid}/financial-summary")]
    public async Task<IActionResult> GetFinancialSummary(
        Guid farmId,
        [FromQuery] Guid? cropId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var summary = await _financialService.GetSummaryAsync(userId, farmId, cropId, fromDate, toDate, ct);
        return Ok(summary);
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
