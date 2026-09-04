namespace SABZ.Application.DTOs.Agronomist;

/// <summary>
/// Structured response from the AI agronomist assistant (Prompt 13).
/// Never exposes UserId, JWT details, provider API keys, or the internal
/// system prompt. The mandatory disclaimer is always present.
/// </summary>
public class AgronomistResponseDto
{
    /// <summary>The question that was answered (text input or voice transcription).</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>The AI-generated agronomy answer (informational only).</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Where the answer came from: "AiProvider" (DashScope) or
    /// "LocalKnowledgeBase" (offline keyword fallback).
    /// </summary>
    public string AnswerSource { get; set; } = "AiProvider";

    /// <summary>Detected/responded language code ("en" or "ur").</summary>
    public string Language { get; set; } = "en";

    /// <summary>The focused farm context that was supplied to the AI.</summary>
    public AgronomistFarmContextDto FarmContextUsed { get; set; } = new();

    /// <summary>Structured limitations / data-context notes.</summary>
    public List<AgronomistLimitationDto> Limitations { get; set; } = new();

    /// <summary>Mandatory advisory disclaimer (always present).</summary>
    public string Disclaimer { get; set; } = string.Empty;

    /// <summary>When this response was generated (UTC).</summary>
    public DateTime GeneratedAt { get; set; }
}
