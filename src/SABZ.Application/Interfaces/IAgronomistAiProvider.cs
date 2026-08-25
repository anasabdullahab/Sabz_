using SABZ.Domain.Exceptions;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Abstraction over the external AI text-generation provider that answers
/// agronomy questions for the voice-first assistant (Prompt 13).
/// Mirrors the existing provider architecture (IWeatherProvider,
/// IPlantDiseaseDetectionProvider): the interface lives in Application, the
/// implementation in Infrastructure. Reuses the shared DashScope/Qwen
/// connection - no separate API key or HTTP plumbing is introduced.
/// </summary>
public interface IAgronomistAiProvider
{
    /// <summary>Whether provider credentials are configured (shared DashScope API key present).</summary>
    bool IsConfigured { get; }

    /// <summary>Provider display name (e.g. "Alibaba Cloud Model Studio (DashScope)").</summary>
    string ProviderName { get; }

    /// <summary>Configured text-generation model identifier (e.g. "qwen-plus").</summary>
    string ModelName { get; }

    /// <summary>
    /// Generate an answer for the given system + user prompts. Throws
    /// <see cref="AgronomistProviderException"/> when the provider is not
    /// configured, unavailable, rate-limited, times out, or returns an
    /// invalid response. Never fabricates an answer.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
