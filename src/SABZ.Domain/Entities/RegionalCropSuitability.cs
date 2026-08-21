namespace SABZ.Domain.Entities;

public class RegionalCropSuitability
{
    public int Id { get; set; }
    public int ProvinceId { get; set; }
    public int? DistrictId { get; set; }

    /// <summary>Optional tehsil-level refinement. Precedence: tehsil > district > province.</summary>
    public int? TehsilId { get; set; }
    public int CropCatalogId { get; set; }
    public required string Season { get; set; }
    public int SuitabilityScore { get; set; }
    public required string SuitabilityLevel { get; set; }
    public string? Notes { get; set; }
    public string? Source { get; set; }

    public Province Province { get; set; } = null!;
    public District? District { get; set; }
    public Tehsil? Tehsil { get; set; }
    public CropCatalog CropCatalog { get; set; } = null!;
}
