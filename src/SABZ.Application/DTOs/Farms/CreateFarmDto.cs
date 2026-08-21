using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Farms;

public class CreateFarmDto
{
    [Required(ErrorMessage = "Farm name is required.")]
    [MinLength(2, ErrorMessage = "Farm name must be at least 2 characters.")]
    public string FarmName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Province is required.")]
    public int? ProvinceId { get; set; }

    [Required(ErrorMessage = "District is required.")]
    public int? DistrictId { get; set; }

    [Required(ErrorMessage = "Tehsil is required.")]
    public int? TehsilId { get; set; }

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? Longitude { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Farm size must be greater than zero.")]
    public decimal FarmSize { get; set; }

    [Required(ErrorMessage = "Farm size unit is required.")]
    public string FarmSizeUnit { get; set; } = "Acres";

    public string? SoilType { get; set; }
    public string? IrrigationType { get; set; }
}
