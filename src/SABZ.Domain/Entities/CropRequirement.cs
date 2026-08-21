namespace SABZ.Domain.Entities;

/// <summary>
/// Structured agricultural requirements for a catalog crop in a specific season.
/// Consumed by the crop suitability scoring engine (data-driven evaluation).
///
/// The initial dataset is the "Initial SABZ suitability dataset" based on general
/// agronomic knowledge; it is not a complete scientific model and should be
/// refined with expert-reviewed / official agricultural sources in future phases.
/// </summary>
public class CropRequirement
{
    public int Id { get; set; }
    public int CropCatalogId { get; set; }

    /// <summary>Agricultural season: "Rabi" or "Kharif".</summary>
    public required string Season { get; set; }

    /// <summary>Typical growing duration in days (sowing to harvest).</summary>
    public int? GrowingDurationDays { get; set; }

    /// <summary>Lower bound of the suitable temperature range (°C).</summary>
    public decimal? MinTempC { get; set; }

    /// <summary>Upper bound of the suitable temperature range (°C).</summary>
    public decimal? MaxTempC { get; set; }

    /// <summary>General water requirement level: "Low", "Medium" or "High".</summary>
    public required string WaterRequirement { get; set; }

    /// <summary>
    /// Compatible soil types as a comma-separated list (e.g. "Loam,Clay Loam").
    /// Foundation format - may move to a relational table in a future phase.
    /// </summary>
    public string? SuitableSoils { get; set; }

    /// <summary>Data provenance note.</summary>
    public string? Source { get; set; }

    public CropCatalog CropCatalog { get; set; } = null!;
}
