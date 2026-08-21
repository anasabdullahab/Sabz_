namespace SABZ.Domain.Exceptions;

/// <summary>
/// Raised by the AI disease-detection pipeline when the external vision
/// provider is not configured, unavailable, times out, rate-limits, or
/// returns an invalid response. Mapped to 502 Bad Gateway - SABZ never
/// fabricates an AI result.
/// </summary>
public class DiseaseProviderException : Exception
{
    public DiseaseProviderException(string message) : base(message) { }
    public DiseaseProviderException(string message, Exception innerException) : base(message, innerException) { }
}
