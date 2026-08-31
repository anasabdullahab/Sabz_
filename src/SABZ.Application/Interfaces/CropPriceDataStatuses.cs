namespace SABZ.Application.Interfaces;

/// <summary>
/// Controlled values for the crop price feature (Prompt 17). Same convention
/// as <c>MarketplaceValues</c>/<c>InputCalculatorValues</c>.
/// </summary>
public static class CropPriceDataStatuses
{
    /// <summary>The provider returned genuinely current data.</summary>
    public const string Live = "Live";

    /// <summary>Dated records from the past, as supplied by the provider.</summary>
    public const string Historical = "Historical";

    /// <summary>
    /// Bundled reference data. Never live - clearly labelled as such.
    /// </summary>
    public const string Reference = "Reference";

    /// <summary>No price data available for the requested crop.</summary>
    public const string Unavailable = "Unavailable";
}
