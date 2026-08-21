namespace SABZ.Application.DTOs.Financial;

/// <summary>
/// A financial transaction as exposed to the API (Prompt 9). Never an EF
/// entity and never exposes the owning UserId - identity always comes from
/// the JWT via the farm.
/// </summary>
public class FinancialTransactionResponseDto
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public Guid? CropId { get; set; }
    public string? CropName { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
