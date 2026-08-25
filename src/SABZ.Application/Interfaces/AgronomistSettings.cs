namespace SABZ.Application.Interfaces;

/// <summary>
/// Configuration for the voice-first AI agronomist assistant (Prompt 13).
///
/// Only agronomist-specific behaviour lives here (model selection and input
/// limits). The provider CONNECTION (API base URL, API key, HTTP timeout) is
/// deliberately NOT duplicated: the agronomist reuses the shared DashScope /
/// Qwen connection already configured for disease detection under the
/// "DiseaseDetection" section, so an operator supplies a single DashScope API
/// key for vision (Prompt 6), agronomist chat and speech-to-text.
/// </summary>
public sealed class AgronomistSettings
{
    public const string SectionName = "Agronomist";

    /// <summary>Text-generation (chat) model used for agronomy answers.</summary>
    public string ChatModel { get; set; } = "qwen-plus";

    /// <summary>Audio-understanding model used for speech-to-text transcription.</summary>
    public string SpeechToTextModel { get; set; } = "qwen2-audio-instruct";

    /// <summary>Maximum allowed length (characters) of a text question.</summary>
    public int MaxQuestionLength { get; set; } = 1000;

    /// <summary>Maximum allowed uploaded audio size in megabytes.</summary>
    public int MaxAudioSizeMb { get; set; } = 10;

    /// <summary>Allowed uploaded audio content types (validated before any provider call).</summary>
    public string[] AllowedAudioTypes { get; set; } =
    {
        "audio/wav",
        "audio/x-wav",
        "audio/wave",
        "audio/vnd.wave",
        "audio/mpeg",
        "audio/mp3",
        "audio/mp4",
        "audio/x-m4a",
        "audio/flac",
        "audio/ogg"
    };

    /// <summary>Maximum number of active crops included in the AI context (bounded context).</summary>
    public int MaxActiveCropsInContext { get; set; } = 10;

    /// <summary>Maximum number of curated disease names included as reference (bounded context).</summary>
    public int MaxDiseaseReferencesInContext { get; set; } = 6;
}
