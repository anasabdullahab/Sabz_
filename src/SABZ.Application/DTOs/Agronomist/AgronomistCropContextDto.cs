namespace SABZ.Application.DTOs.Agronomist;

/// <summary>
/// A single active crop included in the agronomist farm context (Prompt 13).
/// Only crop facts already recorded in SABZ - nothing is invented.
/// </summary>
public class AgronomistCropContextDto
{
    public string CropName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string? GrowthStage { get; set; }
    public string Status { get; set; } = string.Empty;
}
