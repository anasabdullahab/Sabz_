namespace SABZ.Domain.Entities;

/// <summary>
/// Controlled marketplace values (Prompt 15). Plain string constants with
/// HashSet-based validation - the same convention as
/// <see cref="TransactionCategories"/>. Extensible by adding constants;
/// validation is set membership only, anything outside the sets is a 400.
/// </summary>
public static class MarketplaceListingTypes
{
    public const string Sale = "Sale";
    public const string Rent = "Rent";

    public static readonly HashSet<string> All = new() { Sale, Rent };
}

/// <summary>Equipment condition; only these two values are accepted.</summary>
public static class MarketplaceConditions
{
    public const string New = "New";
    public const string Used = "Used";

    public static readonly HashSet<string> All = new() { New, Used };
}

/// <summary>
/// How the informational asking/rental rate is expressed. SABZ never charges
/// anything; the unit only documents what the farmer's price refers to.
/// </summary>
public static class MarketplacePriceUnits
{
    public const string Total = "Total";
    public const string Day = "Day";
    public const string Hour = "Hour";
    public const string Week = "Week";
    public const string Month = "Month";

    public static readonly HashSet<string> All = new() { Total, Day, Hour, Week, Month };
}
