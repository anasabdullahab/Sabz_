using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Financial;

/// <summary>Request body for POST /api/farms/{farmId}/transactions.</summary>
public class CreateFinancialTransactionDto
{
    /// <summary>"Income" or "Expense" (case-insensitive).</summary>
    [Required(ErrorMessage = "Transaction type is required.")]
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>One of the documented categories matching the transaction type.</summary>
    [Required(ErrorMessage = "Category is required.")]
    public string Category { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>When the financial event occurred; future dates are rejected.</summary>
    public DateTime? TransactionDate { get; set; }

    /// <summary>Optional crop of the same farm this entry belongs to.</summary>
    public Guid? CropId { get; set; }

    public string? Notes { get; set; }
}
