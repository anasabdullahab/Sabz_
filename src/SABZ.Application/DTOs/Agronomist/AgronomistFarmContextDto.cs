namespace SABZ.Application.DTOs.Agronomist;

/// <summary>
/// Describes the focused farm context that was actually supplied to the AI
/// agronomist (Prompt 13). This is intentionally a small, relevant subset of
/// farm data (profile + active crops + optional weather) - financial, monitoring
/// and notification history are NOT dumped into the AI prompt. Weather, when
/// present, is clearly marked as external data.
/// </summary>
public class AgronomistFarmContextDto
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Tehsil { get; set; } = string.Empty;
    public string? SoilType { get; set; }
    public string? IrrigationType { get; set; }
    public decimal FarmSize { get; set; }
    public string FarmSizeUnit { get; set; } = string.Empty;

    /// <summary>Active crop records included as context (bounded).</summary>
    public List<AgronomistCropContextDto> ActiveCrops { get; set; } = new();

    /// <summary>Whether external weather was successfully included.</summary>
    public bool WeatherIncluded { get; set; }

    /// <summary>Short external-weather summary (null when not included).</summary>
    public string? WeatherSummary { get; set; }
}
