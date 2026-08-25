namespace SABZ.Application.DTOs.Agronomist;

/// <summary>
/// Voice-flow response (Prompt 13): the speech-to-text transcription plus the
/// same structured agronomist answer. Uploaded audio is processed in memory and
/// never persisted.
/// </summary>
public class VoiceAgronomistResponseDto : AgronomistResponseDto
{
    /// <summary>The transcribed text produced from the uploaded audio.</summary>
    public string Transcription { get; set; } = string.Empty;

    /// <summary>Provider that performed speech-to-text (informational).</summary>
    public string? TranscriptionProvider { get; set; }
}
