namespace SABZ.Domain.Entities;

public class Crop
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public int? CropCatalogId { get; set; }
    public required string CropName { get; set; }
    public required string Season { get; set; }
    public DateTime? PlantingDate { get; set; }

    /// <summary>Date the crop was harvested (set when the crop cycle completes).</summary>
    public DateTime? HarvestDate { get; set; }

    public string? GrowthStage { get; set; }
    public string? PreviousCrop { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Farm Farm { get; set; } = null!;
    public CropCatalog? CropCatalog { get; set; }
}
