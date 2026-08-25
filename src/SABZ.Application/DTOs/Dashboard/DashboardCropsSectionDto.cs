namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// Factual crop overview of the farm (Prompt 12 dashboard section). Counts and
/// summaries come straight from existing crop records - no health,
/// productivity, or yield information is invented.
/// </summary>
public class DashboardCropsSectionDto
{
    public int TotalCrops { get; set; }
    public int ActiveCrops { get; set; }

    /// <summary>Concise summaries of the farm's crop records.</summary>
    public List<DashboardCropItemDto> Crops { get; set; } = [];
}

/// <summary>One factual crop summary line (existing crop fields only).</summary>
public class DashboardCropItemDto
{
    public Guid CropId { get; set; }
    public string CropName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string? GrowthStage { get; set; }

    /// <summary>Existing crop status: Active, Harvested, Failed or Planned.</summary>
    public string Status { get; set; } = string.Empty;
}
