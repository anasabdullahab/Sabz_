namespace SABZ.Domain.Entities;

public class Farm
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string FarmName { get; set; }
    public int ProvinceId { get; set; }
    public int DistrictId { get; set; }
    public int TehsilId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal FarmSize { get; set; }
    public required string FarmSizeUnit { get; set; }
    public string? SoilType { get; set; }
    public string? IrrigationType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Province Province { get; set; } = null!;
    public District District { get; set; } = null!;
    public Tehsil Tehsil { get; set; } = null!;
    public ICollection<Crop> Crops { get; set; } = new List<Crop>();
}
