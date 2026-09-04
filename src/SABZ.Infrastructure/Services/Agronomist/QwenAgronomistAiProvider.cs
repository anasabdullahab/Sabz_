using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SABZ.Application.Interfaces;
using SABZ.Domain.Exceptions;

namespace SABZ.Infrastructure.Services.Agronomist;

/// <summary>
/// Real AI text-generation provider for the voice-first agronomist (Prompt 13):
/// Alibaba Cloud Model Studio (DashScope) OpenAI-compatible chat completions
/// endpoint using a text model (e.g. qwen-plus).
///
/// Reuses the shared DashScope connection already configured for disease
/// detection (Prompt 6) - the SAME ApiBaseUrl and ApiKey under the
/// "DiseaseDetection" section - so no additional API key or HTTP plumbing is
/// introduced. Only the model differs. The provider is fully swappable: SABZ
/// depends only on IAgronomistAiProvider; no provider types leave this class.
/// Never logs API keys or farmer question content.
/// </summary>
public class QwenAgronomistAiProvider : IAgronomistAiProvider
{
    /// <summary>
    /// Named HttpClient configured in DependencyInjection from the shared
    /// DashScope (DiseaseDetection) connection settings. Shared with the
    /// speech-to-text provider.
    /// </summary>
    public const string HttpClientName = "Agronomist";

    private const string ProviderDisplayName = "Alibaba Cloud Model Studio (DashScope)";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiseaseDetectionSettings _connection;
    private readonly AgronomistSettings _settings;
    private readonly ILogger<QwenAgronomistAiProvider> _logger;

    public QwenAgronomistAiProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<DiseaseDetectionSettings> connection,
        IOptions<AgronomistSettings> settings,
        ILogger<QwenAgronomistAiProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _connection = connection.Value;
        _settings = settings.Value;
        _logger = logger;
    }

    // The agronomist shares the DashScope API key with disease detection.
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connection.ApiKey);

    public string ProviderName => ProviderDisplayName;

    public string ModelName => _settings.ChatModel;

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new AgronomistProviderException(
                "The AI agronomist provider is not configured (no API key). " +
                "Set the DashScope DiseaseDetection:ApiKey in local configuration to enable the assistant.");

        var client = _httpClientFactory.CreateClient(HttpClientName);

        var body = new
        {
            model = _settings.ChatModel,
            temperature = 0.3,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var payload = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Fail fast: the chat timeout is deliberately shorter than the shared
        // connection timeout so an unresponsive model does not leave the farmer
        // waiting on the full window. The calling service falls back to the local
        // knowledge base when this throws.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _settings.ChatTimeoutSeconds)));

        string responseBody;
        try
        {
            using var response = await client.PostAsync("chat/completions", content, timeoutCts.Token);
            responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Agronomist AI provider returned HTTP {Status}.", (int)response.StatusCode);
                throw new AgronomistProviderException(response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                        => "The AI provider rejected the configured credentials. Please check the DashScope configuration.",
                    System.Net.HttpStatusCode.TooManyRequests
                        => "The AI provider rate limit was reached. Please try again later.",
                    _
                        => "The AI agronomist provider is currently unavailable. Please try again later."
                });
            }
        }
        catch (AgronomistProviderException)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Agronomist AI provider request timed out.");
            throw new AgronomistProviderException("The AI agronomist provider did not respond in time. Please try again later.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Agronomist AI provider unreachable.");
            throw new AgronomistProviderException("The AI agronomist provider is unreachable. Please try again later.", ex);
        }

        return ParseContent(responseBody);
    }

    /// <summary>Extracts the assistant text; an invalid/empty response never becomes a fake answer.</summary>
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
                throw new AgronomistProviderException("The AI provider returned an empty response. Please try again later.");

            return text.Trim();
        }
        catch (AgronomistProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse agronomist AI provider response.");
            throw new AgronomistProviderException("The AI provider returned an unexpected response format. Please try again later.", ex);
        }
    }
}
