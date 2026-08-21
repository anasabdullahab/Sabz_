using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Financial;

/// <summary>
/// Request body for PUT /api/transactions/{id}. Full replacement semantics:
/// every field is re-validated and ownership (farm/crop) is re-checked.
/// </summary>
public class UpdateFinancialTransactionDto
{
    [Required(ErrorMessage = "Transaction type is required.")]
    public string TransactionType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    public string Category { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime? TransactionDate { get; set; }

    public Guid? CropId { get; set; }

    public string? Notes { get; set; }
}
