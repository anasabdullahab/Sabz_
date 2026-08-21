namespace SABZ.Domain.Entities;

public class Province
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? NameUrdu { get; set; }

    public ICollection<District> Districts { get; set; } = new List<District>();
}
