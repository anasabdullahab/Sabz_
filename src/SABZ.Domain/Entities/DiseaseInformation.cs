namespace SABZ.Domain.Entities;

/// <summary>
/// Curated, data-driven agricultural guidance for known crop diseases.
/// Kept separate from the AI pipeline so advice can be maintained without
/// code changes. Guidance is cautious by design and never prescribes
/// chemical dosages - product labels / local experts are referenced instead.
/// </summary>
public class DiseaseInformation
{
    public int Id { get; set; }
    public required string DiseaseName { get; set; }

    /// <summary>Crop this guidance applies to; null = general guidance.</summary>
    public int? CropCatalogId { get; set; }

    public required string Description { get; set; }
    public required string Symptoms { get; set; }

    /// <summary>Semicolon-separated list of recommended actions.</summary>
    public required string RecommendedActions { get; set; }

    /// <summary>Semicolon-separated list of prevention measures.</summary>
    public required string Prevention { get; set; }

    /// <summary>Semicolon-separated list of monitoring guidance.</summary>
    public required string Monitoring { get; set; }

    public required string Source { get; set; }
    public bool IsActive { get; set; } = true;

    public CropCatalog? CropCatalog { get; set; }
}
