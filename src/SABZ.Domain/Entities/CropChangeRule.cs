namespace SABZ.Domain.Entities;

/// <summary>
/// Data-driven crop-change (rotation) guidance used by the next-crop recommendation engine.
///
/// A rule describes the effect of growing a crop of one catalog category after a crop of
/// another catalog category on the same farm. Rules are keyed by CropCatalog.Category
/// (e.g. "Cereal", "Pulse") rather than individual crops, keeping the model small and
/// extensible; crop-specific rules can be added later without schema changes.
///
/// The initial dataset is the "Initial SABZ crop-change reference dataset" based on
/// general agronomic knowledge - it is NOT a complete scientific model and should be
/// refined with expert-reviewed / official agricultural sources in future phases.
/// </summary>
public class CropChangeRule
{
    public int Id { get; set; }

    /// <summary>CropCatalog.Category of the previously grown crop.</summary>
    public required string PreviousCategory { get; set; }

    /// <summary>CropCatalog.Category of the candidate next crop.</summary>
    public required string NextCategory { get; set; }

    /// <summary>Effect of the change: "Positive", "Caution" or "Negative".</summary>
    public required string Effect { get; set; }

    /// <summary>Farmer-friendly explanation of why this effect applies.</summary>
    public required string Explanation { get; set; }

    /// <summary>Disabled rules are ignored by the recommendation engine.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Data provenance note.</summary>
    public string? Source { get; set; }
}
