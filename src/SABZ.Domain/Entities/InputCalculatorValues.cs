namespace SABZ.Domain.Entities;

/// <summary>
/// Controlled values for the precision crop input &amp; dosage calculator
/// (Prompt 16). Same convention as <see cref="MarketplaceListingTypes"/> and
/// <see cref="TransactionCategories"/>: plain string constants with
/// HashSet-based validation, anything outside the sets is a 400.
///
/// The calculator is pure arithmetic - it never invents or prescribes rates,
/// persists nothing, and never converts between incompatible physical units
/// (kilograms are never turned into liters).
/// </summary>
public static class InputCalculatorAreaUnits
{
    public const string Acres = "Acres";
    public const string Hectares = "Hectares";

    public static readonly HashSet<string> All = new() { Acres, Hectares };
}

/// <summary>What the supplied dosage rate refers to.</summary>
public static class InputCalculatorDosageBases
{
    public const string PerAcre = "PerAcre";
    public const string PerHectare = "PerHectare";

    public static readonly HashSet<string> All = new() { PerAcre, PerHectare };
}

/// <summary>
/// Physical quantity units. Mass and volume units are deliberately kept
/// separate: the calculator never converts between them.
/// </summary>
public static class InputCalculatorQuantityUnits
{
    public const string Kg = "Kg";
    public const string Liters = "Liters";
    public const string Grams = "Grams";
    public const string Milliliters = "Milliliters";

    public static readonly HashSet<string> All = new() { Kg, Liters, Grams, Milliliters };
}

/// <summary>Agricultural input categories; arbitrary values are rejected.</summary>
public static class InputCalculatorCategories
{
    public const string Fertilizer = "Fertilizer";
    public const string Pesticide = "Pesticide";
    public const string Herbicide = "Herbicide";
    public const string Fungicide = "Fungicide";
    public const string Insecticide = "Insecticide";
    public const string Other = "Other";

    public static readonly HashSet<string> All = new() { Fertilizer, Pesticide, Herbicide, Fungicide, Insecticide, Other };
}
