using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SABZ.Application.Interfaces;
using SABZ.Domain.Exceptions;

namespace SABZ.Infrastructure.Services.Agronomist;

/// <summary>
/// Speech-to-text provider for the voice-first agronomist (Prompt 13), built on
/// the SAME DashScope OpenAI-compatible chat infrastructure used for disease
/// detection (Prompt 6) and the agronomist text provider. An audio-understanding
/// model (e.g. qwen2-audio-instruct) receives the uploaded audio as a base64
/// data URL - the exact multimodal pattern the vision provider uses for images.
///
/// This reuses the existing provider/connection infrastructure cleanly instead of
/// introducing a second API key, HTTP stack, or cloud storage. Uploaded audio is
/// sent directly to the provider and never persisted by SABZ. Never logs API keys
/// or audio content.
/// </summary>
public class QwenSpeechToTextProvider : ISpeechToTextProvider
{
    private const string ProviderDisplayName = "Alibaba Cloud Model Studio (DashScope)";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiseaseDetectionSettings _connection;
    private readonly AgronomistSettings _settings;
    private readonly ILogger<QwenSpeechToTextProvider> _logger;

    public QwenSpeechToTextProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<DiseaseDetectionSettings> connection,
        IOptions<AgronomistSettings> settings,
        ILogger<QwenSpeechToTextProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _connection = connection.Value;
        _settings = settings.Value;
        _logger = logger;
    }

    // The agronomist shares the DashScope API key with disease detection.
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connection.ApiKey);

    public string ProviderName => ProviderDisplayName;

    public string ModelName => _settings.SpeechToTextModel;

    public async Task<string> TranscribeAsync(byte[] audioBytes, string contentType, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new AgronomistProviderException(
                "The speech-to-text provider is not configured (no API key). " +
                "Set the DashScope DiseaseDetection:ApiKey in local configuration to enable voice questions.");

        var client = _httpClientFactory.CreateClient(QwenAgronomistAiProvider.HttpClientName);

        var mimeType = string.IsNullOrWhiteSpace(contentType) ? "audio/wav" : contentType;
        var audioDataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(audioBytes)}";

        var body = new
        {
            model = _settings.SpeechToTextModel,
            temperature = 0.0,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "audio_url", audio_url = new { url = audioDataUrl } },
                        new { type = "text", text =
                            "Transcribe this audio exactly in the language it is spoken (English or Urdu). " +
                            "Output ONLY the transcribed text with no commentary, translation, or punctuation notes." }
                    }
                }
            }
        };

        var payload = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        string responseBody;
        try
        {
            using var response = await client.PostAsync("chat/completions", content, ct);
            responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Speech-to-text provider returned HTTP {Status}.", (int)response.StatusCode);
                throw new AgronomistProviderException(response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                        => "The AI provider rejected the configured credentials. Please check the DashScope configuration.",
                    System.Net.HttpStatusCode.TooManyRequests
                        => "The AI provider rate limit was reached. Please try again later.",
                    _
                        => "The speech-to-text provider is currently unavailable. Please try again later."
                });
            }
        }
        catch (AgronomistProviderException)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Speech-to-text provider request timed out.");
            throw new AgronomistProviderException("The speech-to-text provider did not respond in time. Please try again later.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Speech-to-text provider unreachable.");
            throw new AgronomistProviderException("The speech-to-text provider is unreachable. Please try again later.", ex);
        }

        return ParseContent(responseBody);
    }

    /// <summary>Extracts the transcription; an invalid/empty result is never faked.</summary>
    private string ParseContent(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var message = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message");

            var text = message.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String
                ? contentElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(text))
                throw new AgronomistProviderException("The speech-to-text provider could not transcribe the audio. Please try again with a clearer recording.");

            return text.Trim();
        }
        catch (AgronomistProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse speech-to-text provider response.");
            throw new AgronomistProviderException("The speech-to-text provider returned an unexpected response format. Please try again later.", ex);
        }
    }
}
