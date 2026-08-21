namespace SABZ.Domain.Entities;

public class District
{
    public int Id { get; set; }
    public int ProvinceId { get; set; }
    public required string Name { get; set; }
    public string? NameUrdu { get; set; }

    public Province Province { get; set; } = null!;
    public ICollection<Tehsil> Tehsils { get; set; } = new List<Tehsil>();
}
