using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.FertilizerCalculator;

/// <summary>
/// Automated fertilizer presets request (hackathon feature).
/// Only two inputs a farmer knows: crop name and farm size in acres.
/// </summary>
public class FertilizerCalculatorRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string CropName { get; set; } = string.Empty;

    /// <summary>Farm size in acres (1 - 10,000).</summary>
    [Range(0.1, 10000)]
    public decimal FarmSizeAcres { get; set; }
}

/// <summary>
/// Fertilizer plan output: exact bag counts and an application schedule
/// derived from the local crop knowledge base.
/// </summary>
public class FertilizerCalculatorResponseDto
{
    public string CropName { get; set; } = string.Empty;
    public string CropNameUrdu { get; set; } = string.Empty;
    public decimal FarmSizeAcres { get; set; }
    public List<FertilizerApplicationDto> Schedule { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public string Disclaimer { get; set; } = string.Empty;
}

/// <summary>One application step in the fertilizer schedule.</summary>
public class FertilizerApplicationDto
{
    /// <summary>Sowing, First Irrigation, Second Irrigation.</summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>Plain-English timing hint (e.g. "At sowing time", "21-30 days after sowing").</summary>
    public string Timing { get; set; } = string.Empty;

    /// <summary>Recommended products for this stage (DAP / SSP / Urea).</summary>
    public List<FertilizerProductDto> Products { get; set; } = new();
}

/// <summary>One fertilizer product with exact bag counts.</summary>
public class FertilizerProductDto
{
    public string Product { get; set; } = string.Empty;
    public decimal BagsPerAcre { get; set; }
    public decimal TotalBags { get; set; }
    public decimal TotalKg { get; set; }
    public string Application { get; set; } = string.Empty;
}
