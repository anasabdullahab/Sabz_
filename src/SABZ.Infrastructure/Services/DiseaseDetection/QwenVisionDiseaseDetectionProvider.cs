using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SABZ.Application.Interfaces;
using SABZ.Domain.Exceptions;

namespace SABZ.Infrastructure.Services.DiseaseDetection;

/// <summary>
/// Real AI vision provider: Alibaba Cloud Model Studio (DashScope) OpenAI-compatible
/// chat completions endpoint using the Qwen-VL multimodal model.
///
/// A single call performs BOTH the plant/leaf relevance check and the disease
/// assessment (performance requirement: one image = one AI request).
///
/// Free-tier facts (verified against Alibaba Cloud documentation, Aug 2026):
/// - qwen-vl-max / qwen-vl-plus: free quota of 1,000,000 tokens each,
///   Singapore region only, valid for 90 days from activation; pay-as-you-go
///   afterwards unless "Free Quota Only" mode is enabled.
/// - This is a time/quota-limited free tier, NOT permanently free.
///
/// The provider is fully swappable: SABZ only depends on
/// IPlantDiseaseDetectionProvider; no provider types leave this class.
/// Never logs API keys or image content.
/// </summary>
public class QwenVisionDiseaseDetectionProvider : IPlantDiseaseDetectionProvider
{
    public const string HttpClientName = "DiseaseDetection";

    private const string ProviderDisplayName = "Alibaba Cloud Model Studio (DashScope)";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiseaseDetectionSettings _settings;
    private readonly ILogger<QwenVisionDiseaseDetectionProvider> _logger;

    public QwenVisionDiseaseDetectionProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<DiseaseDetectionSettings> settings,
        ILogger<QwenVisionDiseaseDetectionProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public string ProviderName => ProviderDisplayName;

    public string ModelName => _settings.Model;

    // The OpenAI-compatible endpoint does not expose a model version.
    public string? ModelVersion => null;

    public async Task<PlantDiseaseDetectionResult> DetectAsync(
        PlantDiseaseDetectionRequest request,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new DiseaseProviderException(
                "The AI disease-detection provider is not configured (no API key). " +
                "Set DiseaseDetection:ApiKey in local configuration to enable live analysis.");

        var client = _httpClientFactory.CreateClient(HttpClientName);

        var payload = BuildRequestPayload(request);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        string responseBody;
        try
        {
            using var response = await client.PostAsync("chat/completions", content, ct);

            responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Disease detection provider returned HTTP {Status}.", (int)response.StatusCode);
                throw new DiseaseProviderException(response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                        => "The AI provider rejected the configured credentials. Please check the DiseaseDetection configuration.",
                    System.Net.HttpStatusCode.TooManyRequests
                        => "The AI provider rate limit was reached. Please try again later.",
                    _
                        => "The AI disease-detection provider is currently unavailable. Please try again later."
                });
            }
        }
        catch (DiseaseProviderException)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Disease detection provider request timed out.");
            throw new DiseaseProviderException("The AI disease-detection provider did not respond in time. Please try again later.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Disease detection provider unreachable.");
            throw new DiseaseProviderException("The AI disease-detection provider is unreachable. Please try again later.", ex);
        }

        return ParseResponse(responseBody);
    }

    // ------------------------------------------------------------------
    //  Request
    // ------------------------------------------------------------------

    private string BuildRequestPayload(PlantDiseaseDetectionRequest request)
    {
        var imageDataUrl = $"data:{request.ImageMimeType};base64,{Convert.ToBase64String(request.ImageBytes)}";

        var promptBuilder = new StringBuilder();
        promptBuilder.Append(
            "You are a cautious agricultural image analyst. First decide whether the photograph shows a crop, " +
            "plant or leaf. If it clearly does not (sky, road, wall, person, animal, machinery, building, screenshot, " +
            "or any unrelated subject), set isPlantImage=false and do not report any disease. " +
            "If it does show a plant, carefully assess whether it shows signs of disease, pest damage, nutrient " +
            "deficiency, or appears healthy. Never invent a disease; if uncertain, say so with low confidence.");

        if (request.CropNameHint is not null)
            promptBuilder.Append($" Farmer context - crop: {request.CropNameHint}.");
        if (request.CropCategoryHint is not null)
            promptBuilder.Append($" Crop category: {request.CropCategoryHint}.");
        if (request.SeasonHint is not null)
            promptBuilder.Append($" Season: {request.SeasonHint}.");
        if (request.GrowthStageHint is not null)
            promptBuilder.Append($" Growth stage: {request.GrowthStageHint}.");
        if (request.WeatherContextHint is not null)
            promptBuilder.Append($" {request.WeatherContextHint}");
        if (!string.IsNullOrWhiteSpace(request.FarmerNotes))
            promptBuilder.Append($" Farmer notes: {request.FarmerNotes}");

        promptBuilder.Append(
            " Respond ONLY with a single JSON object (no markdown, no code fences) using exactly these fields: " +
            "{ \"isPlantImage\": boolean, \"plantConfidence\": number 0-1, \"plantReason\": string, " +
            "\"detectedCrop\": string or null, \"hasDisease\": boolean, \"disease\": string or null, " +
            "\"diseaseConfidence\": number 0-1, \"severity\": \"mild\" or \"moderate\" or \"severe\" or null, " +
            "\"explanation\": string }. " +
            "Use cautious language in text fields. If the plant looks healthy, set hasDisease=false and disease=null.");

        var body = new
        {
            model = _settings.Model,
            temperature = 0.1,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image_url", image_url = new { url = imageDataUrl } },
                        new { type = "text", text = promptBuilder.ToString() }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(body, JsonOptions);
    }

    // ------------------------------------------------------------------
    //  Response parsing (defensive; invalid responses never become fake results)
    // ------------------------------------------------------------------

    private PlantDiseaseDetectionResult ParseResponse(string responseBody)
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
                throw new DiseaseProviderException("The AI provider returned an empty response. Please try again later.");

            using var analysis = JsonDocument.Parse(StripCodeFences(text));
            var root = analysis.RootElement;

            var isPlant = GetBool(root, "isPlantImage");
            var plantConfidence = Clamp01(GetDouble(root, "plantConfidence"));
            var hasDisease = GetBool(root, "hasDisease");
            var diseaseConfidence = Clamp01(GetDouble(root, "diseaseConfidence"));

            return new PlantDiseaseDetectionResult
            {
                IsPlantImage = isPlant,
                PlantConfidence = plantConfidence,
                PlantReason = GetString(root, "plantReason"),
                DetectedCrop = GetString(root, "detectedCrop"),
                DiseaseDetected = isPlant && hasDisease && !string.IsNullOrWhiteSpace(GetString(root, "disease")),
                DiseaseName = GetString(root, "disease"),
                DiseaseConfidence = diseaseConfidence,
                Severity = GetString(root, "severity"),
                Explanation = GetString(root, "explanation"),
                ProviderName = ProviderDisplayName,
                ModelName = _settings.Model,
                ModelVersion = ModelVersion
            };
        }
        catch (DiseaseProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse disease detection provider response.");
            throw new DiseaseProviderException("The AI provider returned an unexpected response format. Please try again later.", ex);
        }
    }

    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
                trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
                trimmed = trimmed[..^3];
        }
        return trimmed.Trim();
    }

    private static bool GetBool(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.True;

    private static double GetDouble(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetDouble() : 0.0;

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
