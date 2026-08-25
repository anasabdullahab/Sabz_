namespace SABZ.Application.DTOs.Dashboard;

/// <summary>
/// Factual farm information already stored by SABZ (Prompt 12 dashboard
/// section). Never exposes the owner's UserId - identity always comes from
/// the JWT.
/// </summary>
public class DashboardFarmSectionDto
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Tehsil { get; set; } = string.Empty;
    public decimal FarmSize { get; set; }
    public string FarmSizeUnit { get; set; } = string.Empty;
    public string? SoilType { get; set; }
    public string? IrrigationType { get; set; }

    /// <summary>GPS coordinates recorded by the farmer (weather needs both).</summary>
    public bool HasCoordinates { get; set; }
}
