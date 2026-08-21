using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Crops;

public class CreateCropDto
{
    [Required(ErrorMessage = "Crop name is required.")]
    [MinLength(2, ErrorMessage = "Crop name must be at least 2 characters.")]
    public string CropName { get; set; } = string.Empty;

    public int? CropCatalogId { get; set; }

    [Required(ErrorMessage = "Season is required.")]
    public string Season { get; set; } = string.Empty;

    public DateTime? PlantingDate { get; set; }
    public string? GrowthStage { get; set; }
    public string? PreviousCrop { get; set; }
    public string? Status { get; set; }
}
