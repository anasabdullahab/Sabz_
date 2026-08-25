using SABZ.Domain.Exceptions;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Abstraction over the external speech-to-text capability used by the
/// voice-first agronomist (Prompt 13). The interface lives in Application,
/// the implementation in Infrastructure. It reuses the shared DashScope/Qwen
/// connection and the existing multimodal chat pattern (audio is supplied as
/// a base64 data URL, exactly like the vision provider supplies images), so no
/// separate API key, HTTP client, or storage is introduced.
/// </summary>
public interface ISpeechToTextProvider
{
    /// <summary>Whether provider credentials are configured (shared DashScope API key present).</summary>
    bool IsConfigured { get; }

    /// <summary>Provider display name (e.g. "Alibaba Cloud Model Studio (DashScope)").</summary>
    string ProviderName { get; }

    /// <summary>Configured audio-understanding model identifier (e.g. "qwen2-audio-instruct").</summary>
    string ModelName { get; }

    /// <summary>
    /// Transcribe the uploaded audio bytes into text. Throws
    /// <see cref="AgronomistProviderException"/> when the provider is not
    /// configured, unavailable, rate-limited, times out, or returns an
    /// invalid/empty response. Never fabricates a transcription.
    /// </summary>
    Task<string> TranscribeAsync(byte[] audioBytes, string contentType, CancellationToken ct = default);
}
