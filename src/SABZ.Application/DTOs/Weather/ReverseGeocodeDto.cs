namespace SABZ.Application.DTOs.Weather;

/// <summary>
/// Result of a reverse geocode look-up: coordinates → human-readable place name.
/// </summary>
public class ReverseGeocodeDto
{
    /// <summary>Primary place name (e.g. city or town).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Administrative region (e.g. district or province).</summary>
    public string? Admin1 { get; set; }

    /// <summary>Country name.</summary>
    public string? Country { get; set; }

    /// <summary>Combined display label (e.g. "Lahore, Punjab").</summary>
    public string DisplayLabel { get; set; } = string.Empty;
}
