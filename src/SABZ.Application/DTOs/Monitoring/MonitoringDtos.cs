namespace SABZ.Application.DTOs.Monitoring;

/// <summary>
/// A monitoring check as exposed to the API (Prompt 7). Never an EF entity.
/// "Status" is the farmer-facing status: Upcoming / Due / Completed / Skipped.
/// </summary>
public class MonitoringCheckDto
{
    public Guid Id { get; set; }
    public Guid CropId { get; set; }
    public string CropName { get; set; } = string.Empty;
    public string? CropCatalogName { get; set; }
    public Guid FarmId { get; set; }
    public string? FarmName { get; set; }

    public DateTime ScheduledDate { get; set; }

    /// <summary>Upcoming, Due, Completed or Skipped (computed consistently in UTC).</summary>
    public string Status { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> InspectionItems { get; set; } = new();
    public string Priority { get; set; } = "Medium";

    /// <summary>Farmer observation if completed (Normal / SomethingSuspicious); never a diagnosis.</summary>
    public string? Observation { get; set; }

    public string? FarmerNotes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }

    /// <summary>True only for completed checks reported as SomethingSuspicious.</summary>
    public bool PhotoAnalysisRecommended { get; set; }
}

/// <summary>Request body for completing a monitoring check.</summary>
public class CompleteMonitoringCheckRequestDto
{
    /// <summary>Must be exactly "Normal" or "SomethingSuspicious".</summary>
    public string Observation { get; set; } = string.Empty;

    public string? Notes { get; set; }
}

/// <summary>Request body for skipping a monitoring check.</summary>
public class SkipMonitoringCheckRequestDto
{
    public string? Notes { get; set; }
}

/// <summary>Response after completing a check, including the Prompt 6 photo hand-off.</summary>
public class MonitoringCompletionResponseDto
{
    public MonitoringCheckDto Check { get; set; } = new();

    /// <summary>True only when the farmer reported SomethingSuspicious.</summary>
    public bool PhotoAnalysisRecommended { get; set; }

    /// <summary>Guidance on the next step (points at the existing disease-detection workflow).</summary>
    public string NextAction { get; set; } = string.Empty;

    /// <summary>Always present: an observation is the farmer's report, not a diagnosis.</summary>
    public string ObservationNote { get; set; } = string.Empty;
}

/// <summary>Result of (idempotent) monitoring check generation for a crop.</summary>
public class MonitoringGenerationResultDto
{
    public Guid CropId { get; set; }
    public bool HasPlantingDate { get; set; }
    public DateTime? PlantingDate { get; set; }

    /// <summary>Applicable rules considered during this run.</summary>
    public int RulesApplied { get; set; }

    /// <summary>Checks created by this run (0 when everything already exists).</summary>
    public int ChecksCreated { get; set; }

    /// <summary>Checks already present for this crop (never duplicated).</summary>
    public int ExistingChecks { get; set; }

    public List<MonitoringCheckDto> Checks { get; set; } = new();

    public List<string> Notes { get; set; } = new();
}
