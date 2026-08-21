namespace SABZ.Domain.Entities;

/// <summary>
/// Direction of a financial ledger entry (Prompt 9). Persisted as a string
/// (existing SABZ enum convention); the closed set drives P&L sign maths.
/// </summary>
public enum FinancialTransactionType
{
    Income = 1,
    Expense = 2
}

/// <summary>
/// A farmer-entered financial ledger entry (Prompt 9).
///
/// Ownership is never stored on the transaction itself - it is derived through
/// Farm (Farm.UserId), so clients can never submit a UserId/OwnerId. A
/// transaction is farm-level by default and may optionally reference one crop
/// of that farm, which later allows crop-level profitability analysis.
///
/// Transactions are only ever created/updated by the farmer through the API;
/// the system never invents financial data.
/// </summary>
public class FinancialTransaction
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }

    /// <summary>Optional crop of the same farm this entry belongs to.</summary>
    public Guid? CropId { get; set; }

    public FinancialTransactionType TransactionType { get; set; }

    /// <summary>Farmer-facing category (see TransactionCategories).</summary>
    public required string Category { get; set; }

    /// <summary>Positive amount in PKR, stored as decimal(18,2).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// When the financial event occurred (farmer-supplied date, stored as UTC
    /// midnight). Distinct from CreatedAt, which records when SABZ stored it.
    /// </summary>
    public DateTime TransactionDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Farm Farm { get; set; } = null!;
    public Crop? Crop { get; set; }
}
