namespace SABZ.Domain.Exceptions;

/// <summary>
/// Raised by the voice-first AI agronomist pipeline (Prompt 13) when the
/// external AI text provider or the speech-to-text provider is not
/// configured, unavailable, times out, rate-limits, or returns an invalid
/// response. Mapped to 502 Bad Gateway - SABZ never fabricates an answer or
/// a transcription.
/// </summary>
public class AgronomistProviderException : Exception
{
    public AgronomistProviderException(string message) : base(message) { }
    public AgronomistProviderException(string message, Exception innerException) : base(message, innerException) { }
}
