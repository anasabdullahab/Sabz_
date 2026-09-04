namespace SABZ.Application.DTOs.Locations;

public class LocationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameUrdu { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
