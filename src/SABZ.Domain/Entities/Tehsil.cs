namespace SABZ.Domain.Entities;

public class Tehsil
{
    public int Id { get; set; }
    public int DistrictId { get; set; }
    public required string Name { get; set; }
    public string? NameUrdu { get; set; }

    public District District { get; set; } = null!;
}
