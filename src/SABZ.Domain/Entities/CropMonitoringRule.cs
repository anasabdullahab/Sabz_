namespace SABZ.Domain.Entities;

/// <summary>
/// Data-driven monitoring checkpoint definition for a crop (Prompt 7).
/// Rules come from reference data, never hard-coded workflow switches.
///
/// <see cref="TriggerType"/> is kept as a plain string so future trigger
/// kinds (WeatherEvent, SatelliteAlert, Manual) can be added without a
/// schema migration. Only "Scheduled" rules are generated today.
/// </summary>
public class CropMonitoringRule
{
    public int Id { get; set; }

    /// <summary>Crop this rule applies to. Null = applies to every crop.</summary>
    public int? CropCatalogId { get; set; }

    /// <summary>Days after planting when the check should be performed.</summary>
    public int DayOffsetAfterPlanting { get; set; }

    public required string Title { get; set; }
    public required string Description { get; set; }

    /// <summary>What the farmer should inspect (semicolon-separated list).</summary>
    public required string InspectionItems { get; set; }

    /// <summary>Low / Medium / High.</summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>Future trigger kinds: Scheduled, WeatherEvent, SatelliteAlert, Manual.</summary>
    public string TriggerType { get; set; } = "Scheduled";

    public bool IsActive { get; set; } = true;
    public required string Source { get; set; }

    public CropCatalog? CropCatalog { get; set; }
}
