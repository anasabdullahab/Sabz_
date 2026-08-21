using SABZ.Domain.Exceptions;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Abstraction over the external AI vision provider that performs
/// plant/leaf relevance checking and crop disease identification.
/// Mirrors the existing weather-provider architecture: the interface
/// lives in Application, the implementation in Infrastructure.
/// A single call performs both the relevance check and the disease
/// assessment so one uploaded image triggers exactly one AI request.
/// </summary>
public interface IPlantDiseaseDetectionProvider
{
    /// <summary>Whether provider credentials are configured (API key present).</summary>
    bool IsConfigured { get; }

    /// <summary>Provider display name (e.g. "Alibaba Cloud Model Studio (DashScope)").</summary>
    string ProviderName { get; }

    /// <summary>Configured model identifier (e.g. "qwen-vl-max").</summary>
    string ModelName { get; }

    /// <summary>Model version if the provider exposes one; otherwise null.</summary>
    string? ModelVersion { get; }

    /// <summary>
    /// Analyse the image. Throws <see cref="DiseaseProviderException"/> when the
    /// provider is not configured, unavailable, rate-limited, times out, or
    /// returns an invalid response. Never fabricates a result.
    /// </summary>
    Task<PlantDiseaseDetectionResult> DetectAsync(
        PlantDiseaseDetectionRequest request,
        CancellationToken ct = default);
}
