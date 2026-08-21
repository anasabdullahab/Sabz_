namespace SABZ.Application.DTOs.Crops;

public class CropResponseDto
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public int? CropCatalogId { get; set; }
    public string CropName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public DateTime? PlantingDate { get; set; }
    public string? GrowthStage { get; set; }
    public string? PreviousCrop { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
