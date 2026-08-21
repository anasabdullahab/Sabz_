namespace SABZ.Application.DTOs.Farms;

public class FarmResponseDto
{
    public Guid Id { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public int ProvinceId { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
    public int DistrictId { get; set; }
    public string DistrictName { get; set; } = string.Empty;
    public int TehsilId { get; set; }
    public string TehsilName { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal FarmSize { get; set; }
    public string FarmSizeUnit { get; set; } = string.Empty;
    public string? SoilType { get; set; }
    public string? IrrigationType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
