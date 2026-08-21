namespace SABZ.Domain.Entities;

/// <summary>
/// Farmer-facing financial categories (Prompt 9). Plain string constants with
/// HashSet-based validation - data-driven, no switch statements, no crop names
/// in financial logic. Extensible by adding constants; validation is set
/// membership only.
/// </summary>
public static class TransactionCategories
{
    // Expenses
    public const string Seeds = "Seeds";
    public const string Fertilizer = "Fertilizer";
    public const string Labour = "Labour";
    public const string Irrigation = "Irrigation";
    public const string Equipment = "Equipment";
    public const string Machinery = "Machinery";
    public const string Fuel = "Fuel";
    public const string Transport = "Transport";
    public const string PestDiseaseManagement = "PestDiseaseManagement";
    public const string OtherExpense = "OtherExpense";

    // Income
    public const string CropSale = "CropSale";
    public const string LivestockIncome = "LivestockIncome";
    public const string OtherIncome = "OtherIncome";

    public static readonly HashSet<string> ExpenseCategories = new()
    {
        Seeds, Fertilizer, Labour, Irrigation, Equipment,
        Machinery, Fuel, Transport, PestDiseaseManagement, OtherExpense
    };

    public static readonly HashSet<string> IncomeCategories = new()
    {
        CropSale, LivestockIncome, OtherIncome
    };

    /// <summary>The category must exist AND match the declared transaction type.</summary>
    public static bool IsValidFor(string category, FinancialTransactionType type)
    {
        var allowed = type == FinancialTransactionType.Income ? IncomeCategories : ExpenseCategories;
        return allowed.Contains(category);
    }

    /// <summary>All known categories (for documentation and diagnostics).</summary>
    public static IReadOnlyCollection<string> All { get; } =
        new HashSet<string>(ExpenseCategories.Concat(IncomeCategories));
}
