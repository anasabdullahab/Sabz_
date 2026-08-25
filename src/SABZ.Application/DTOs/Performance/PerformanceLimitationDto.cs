namespace SABZ.Application.DTOs.Performance;

/// <summary>
/// A structured, factual limitation of the recorded data behind a farm
/// performance response (Prompt 11). Limitations are never hidden in free
/// text - each has a stable machine-readable Code plus a human message.
/// </summary>
public class PerformanceLimitationDto
{
    /// <summary>Stable limitation code (e.g. NoFinancialTransactions).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Factual description of the limitation, based only on recorded data.</summary>
    public string Message { get; set; } = string.Empty;
}
