namespace SABZ.Application.DTOs.CropSuitability;

/// <summary>
/// Top-level crop suitability evaluation response for a farm.
/// </summary>
public class CropSuitabilityResponseDto
{
    public Guid FarmId { get; set; }
    public FarmLocationDto Location { get; set; } = new();

    /// <summary>Season used for the evaluation ("Rabi" or "Kharif").</summary>
    public string EvaluationSeason { get; set; } = string.Empty;

    /// <summary>How the season was chosen: "ClientProvided" or "AutoDetected".</summary>
    public string SeasonSource { get; set; } = string.Empty;

    public DateTime EvaluatedAt { get; set; }

    /// <summary>Whether usable weather data was available for climate scoring.</summary>
    public bool WeatherDataAvailable { get; set; }

    /// <summary>Evaluated crops, sorted by suitability score (highest first).</summary>
    public List<CropSuitabilityResultDto> Crops { get; set; } = new();

    /// <summary>
    /// The scores are SABZ suitability evaluations based on the currently available
    /// data model - they are not guaranteed agricultural outcomes.
    /// </summary>
    public string Disclaimer { get; set; } =
        "SABZ suitability evaluation based on currently available data. Not a guaranteed agricultural outcome.";
}

/// <summary>Province / district / tehsil names of the evaluated farm.</summary>
public class FarmLocationDto
{
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Tehsil { get; set; } = string.Empty;
}
