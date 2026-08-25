namespace SABZ.Application.DTOs.Agronomist;

/// <summary>
/// A structured, factual limitation/data-context note returned by the
/// agronomist assistant (Prompt 13). Mirrors the dashboard limitation shape:
/// a stable machine-readable Code plus a human message, never hidden in free text.
/// </summary>
public class AgronomistLimitationDto
{
    /// <summary>Stable limitation code (e.g. RecordedDataOnly, WeatherUnavailable).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Factual description based only on recorded SABZ data or external sources.</summary>
    public string Message { get; set; } = string.Empty;
}
