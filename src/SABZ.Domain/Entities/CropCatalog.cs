namespace SABZ.Domain.Entities;

public class CropCatalog
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? ScientificName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }

    public ICollection<RegionalCropSuitability> RegionalSuitabilities { get; set; } = new List<RegionalCropSuitability>();
}
