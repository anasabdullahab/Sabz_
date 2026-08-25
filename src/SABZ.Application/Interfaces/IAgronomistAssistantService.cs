using SABZ.Application.DTOs.Agronomist;

namespace SABZ.Application.Interfaces;

/// <summary>
/// Voice-first AI agronomist assistant (Prompt 13). An information-and-guidance
/// assistant only: it NEVER creates or modifies farms, crops, transactions,
/// monitoring checks, notifications, users, or any persisted SABZ data, and it
/// never persists chat history. Implemented in Application; external AI/STT
/// providers are abstracted behind interfaces and live in Infrastructure.
/// </summary>
public interface IAgronomistAssistantService
{
    /// <summary>
    /// Answer a text agriculture question for an owned farm.
    /// Ownership: unknown farm -> 404, another user's farm -> 403.
    /// Empty/overlong question -> 400. Provider not configured/unavailable -> 502.
    /// </summary>
    Task<AgronomistResponseDto> ChatAsync(
        Guid userId,
        Guid farmId,
        TextAgronomistQuestionDto request,
        CancellationToken ct = default);

    /// <summary>
    /// Transcribe an uploaded voice question (in memory, never stored) and answer it.
    /// Ownership: unknown farm -> 404, another user's farm -> 403.
    /// Missing/empty/oversized/unsupported audio -> 400. Provider not configured -> 502.
    /// </summary>
    Task<VoiceAgronomistResponseDto> VoiceAsync(
        Guid userId,
        Guid farmId,
        byte[] audioBytes,
        string? contentType,
        string? fileName,
        CancellationToken ct = default);
}
