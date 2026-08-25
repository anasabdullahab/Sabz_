using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SABZ.Application.DTOs.Agronomist;
using SABZ.Application.DTOs.Weather;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Agronomist;

/// <summary>
/// Voice-first AI Agronomist Assistant (Prompt 13).
///
/// Design decisions:
/// - Information &amp; guidance ONLY. This service is strictly read-only: it never
///   creates or modifies farms, crops, transactions, monitoring checks,
///   notifications, users, or any persisted SABZ data, and it never persists
///   chat history. Uploaded voice audio is processed in memory only.
/// - Reuses the existing AI/provider infrastructure: the text answer and the
///   speech-to-text transcription both ride the shared DashScope connection
///   already used by disease detection (Prompt 6); ownership, validation, the
///   weather abstraction and curated disease reference are all reused too.
/// - Builds a FOCUSED farm context (profile + active crops + optional weather +
///   optional curated disease reference) rather than dumping all farm data.
/// - Clearly distinguishes recorded SABZ data, farmer-provided information and
///   external weather. Never invents data; missing information is reported.
/// - The mandatory advisory disclaimer is always present on a successful answer.
/// </summary>
public class AgronomistAssistantService : IAgronomistAssistantService
{
    // Structured limitation codes (stable, factual).
    public const string LimitRecordedDataOnly = "RecordedDataOnly";
    public const string LimitNoCrops = "NoCrops";
    public const string LimitNoCoordinates = "NoCoordinates";
    public const string LimitWeatherUnavailable = "WeatherUnavailable";

    /// <summary>Mandatory advisory statement (always present on a successful answer).</summary>
    public const string Disclaimer =
        "The SABZ AI Agronomist provides informational assistance based on the farmer's question and available SABZ or " +
        "external data. It does not physically inspect the farm, guarantee outcomes, automatically diagnose diseases, " +
        "or perform actions on behalf of the farmer.";

    private static readonly string[] DiseaseKeywords =
    {
        "disease", "symptom", "pest", "fungus", "fungal", "spot", "spots", "yellow", "wilt", "wilting",
        "leaf", "leaves", "insect", "attack", "blight", "rust", "mildew", "sick", "rot", "damage",
        "بیماری", "کیڑ", "کیڑے", "پتی", "پتے", "پیلی"
    };

    private readonly IFarmRepository _farmRepository;
    private readonly ICropRepository _cropRepository;
    private readonly IWeatherService _weatherService;
    private readonly IDiseaseInformationRepository _diseaseInformation;
    private readonly IAgronomistAiProvider _aiProvider;
    private readonly ISpeechToTextProvider _speechToTextProvider;
    private readonly AgronomistSettings _settings;
    private readonly ISystemClock _clock;
    private readonly ILogger<AgronomistAssistantService> _logger;

    public AgronomistAssistantService(
        IFarmRepository farmRepository,
        ICropRepository cropRepository,
        IWeatherService weatherService,
        IDiseaseInformationRepository diseaseInformation,
        IAgronomistAiProvider aiProvider,
        ISpeechToTextProvider speechToTextProvider,
        IOptions<AgronomistSettings> settings,
        ISystemClock clock,
        ILogger<AgronomistAssistantService> logger)
    {
        _farmRepository = farmRepository;
        _cropRepository = cropRepository;
        _weatherService = weatherService;
        _diseaseInformation = diseaseInformation;
        _aiProvider = aiProvider;
        _speechToTextProvider = speechToTextProvider;
        _settings = settings.Value;
        _clock = clock;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    //  Text question
    // ------------------------------------------------------------------

    public async Task<AgronomistResponseDto> ChatAsync(
        Guid userId,
        Guid farmId,
        TextAgronomistQuestionDto request,
        CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId);

        var question = request?.Message?.Trim();
        ValidateQuestion(question);

        return await BuildAnswerAsync(userId, farm, question!, ct);
    }

    // ------------------------------------------------------------------
    //  Voice question (in-memory transcription, never stored)
    // ------------------------------------------------------------------

    public async Task<VoiceAgronomistResponseDto> VoiceAsync(
        Guid userId,
        Guid farmId,
        byte[] audioBytes,
        string? contentType,
        string? fileName,
        CancellationToken ct = default)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId);

        ValidateAudio(audioBytes, contentType);

        // Speech-to-text provider gate (never fakes a transcription).
        if (!_speechToTextProvider.IsConfigured)
            throw new AgronomistProviderException(
                "The speech-to-text provider is not configured. " +
                "Set the DashScope DiseaseDetection:ApiKey in local configuration to enable voice questions.");

        var transcription = await _speechToTextProvider.TranscribeAsync(
            audioBytes, contentType ?? "audio/wav", ct);

        var question = transcription.Trim();
        if (question.Length == 0)
            throw new ValidationException("No speech could be understood from the audio. Please record your question again clearly.");
        if (question.Length > _settings.MaxQuestionLength)
            question = question[.._settings.MaxQuestionLength];

        var answer = await BuildAnswerAsync(userId, farm, question, ct);

        return new VoiceAgronomistResponseDto
        {
            Transcription = transcription,
            TranscriptionProvider = _speechToTextProvider.ProviderName,
            Question = answer.Question,
            Answer = answer.Answer,
            Language = answer.Language,
            FarmContextUsed = answer.FarmContextUsed,
            Limitations = answer.Limitations,
            Disclaimer = answer.Disclaimer,
            GeneratedAt = answer.GeneratedAt
        };
    }

    // ------------------------------------------------------------------
    //  Shared answer pipeline (read-only)
    // ------------------------------------------------------------------

    private async Task<AgronomistResponseDto> BuildAnswerAsync(
        Guid userId,
        Farm farm,
        string question,
        CancellationToken ct)
    {
        var language = DetectLanguage(question);

        // AI provider gate (never fabricates an answer).
        if (!_aiProvider.IsConfigured)
            throw new AgronomistProviderException(
                "The AI agronomist provider is not configured. " +
                "Set the DashScope DiseaseDetection:ApiKey in local configuration to enable the assistant.");

        var (farmContext, limitations, contextBlock) = await BuildFarmContextAsync(userId, farm, question, ct);

        var systemPrompt = BuildSystemPrompt(language);
        var userPrompt = BuildUserPrompt(question, contextBlock);

        var answer = await _aiProvider.CompleteAsync(systemPrompt, userPrompt, ct);

        return new AgronomistResponseDto
        {
            Question = question,
            Answer = answer,
            Language = language,
            FarmContextUsed = farmContext,
            Limitations = limitations,
            Disclaimer = Disclaimer,
            GeneratedAt = _clock.UtcNow
        };
    }

    // ------------------------------------------------------------------
    //  Ownership (existing SABZ pattern - JWT user id only)
    // ------------------------------------------------------------------

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return farm;
    }

    // ------------------------------------------------------------------
    //  Input validation
    // ------------------------------------------------------------------

    private void ValidateQuestion(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ValidationException("A question is required. Please ask an agriculture-related question.");

        if (question.Length > _settings.MaxQuestionLength)
            throw new ValidationException(
                $"The question is too long. Please keep it under {_settings.MaxQuestionLength} characters.");
    }

    private void ValidateAudio(byte[]? audioBytes, string? contentType)
    {
        if (audioBytes is null || audioBytes.Length == 0)
            throw new ValidationException("An audio file is required. Please record and upload your voice question.");

        var maxBytes = (long)_settings.MaxAudioSizeMb * 1024 * 1024;
        if (audioBytes.Length > maxBytes)
            throw new ValidationException(
                $"The audio file is too large. Please keep it under {_settings.MaxAudioSizeMb} MB.");

        if (string.IsNullOrWhiteSpace(contentType)
            || !_settings.AllowedAudioTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "Unsupported audio format. Please upload one of: " + string.Join(", ", _settings.AllowedAudioTypes) + ".");
        }
    }

    // ------------------------------------------------------------------
    //  Focused farm context (bounded; never dumps all farm data)
    // ------------------------------------------------------------------

    private async Task<(AgronomistFarmContextDto context, List<AgronomistLimitationDto> limitations, string contextBlock)>
        BuildFarmContextAsync(Guid userId, Farm farm, string question, CancellationToken ct)
    {
        var limitations = new List<AgronomistLimitationDto>();

        var context = new AgronomistFarmContextDto
        {
            FarmId = farm.Id,
            FarmName = farm.FarmName,
            Province = farm.Province.Name,
            District = farm.District.Name,
            Tehsil = farm.Tehsil.Name,
            SoilType = farm.SoilType,
            IrrigationType = farm.IrrigationType,
            FarmSize = farm.FarmSize,
            FarmSizeUnit = farm.FarmSizeUnit
        };

        // Active crops (bounded).
        var crops = await _cropRepository.GetByFarmIdAsync(farm.Id);
        var activeCrops = crops
            .Where(c => c.Status == "Active")
            .Take(_settings.MaxActiveCropsInContext)
            .ToList();
        context.ActiveCrops = activeCrops
            .Select(c => new AgronomistCropContextDto
            {
                CropName = c.CropName,
                Season = c.Season,
                GrowthStage = c.GrowthStage,
                Status = c.Status
            })
            .ToList();

        if (activeCrops.Count == 0)
            limitations.Add(new AgronomistLimitationDto
            {
                Code = LimitNoCrops,
                Message = "This farm has no active crop records in SABZ, so the answer cannot reference specific recorded crops."
            });

        // Optional external weather (never breaks the answer).
        if (farm.Latitude.HasValue && farm.Longitude.HasValue)
        {
            try
            {
                var weather = await _weatherService.GetCurrentWeatherAsync(userId, farm.Id, ct);
                var summary = SummarizeWeather(weather);
                if (summary is not null)
                {
                    context.WeatherIncluded = true;
                    context.WeatherSummary = summary;
                }
                else
                {
                    limitations.Add(new AgronomistLimitationDto
                    {
                        Code = LimitWeatherUnavailable,
                        Message = "Current weather could not be read from the provider response; the answer uses recorded SABZ data only."
                    });
                }
            }
            catch (Exception ex) when (ex is not ForbiddenException and not NotFoundException)
            {
                _logger.LogWarning(ex, "Agronomist weather unavailable for farm {FarmId}; continuing without weather.", farm.Id);
                limitations.Add(new AgronomistLimitationDto
                {
                    Code = LimitWeatherUnavailable,
                    Message = "Current weather could not be retrieved; the answer is based on recorded SABZ data only."
                });
            }
        }
        else
        {
            limitations.Add(new AgronomistLimitationDto
            {
                Code = LimitNoCoordinates,
                Message = "This farm has no GPS coordinates recorded, so external weather was not used."
            });
        }

        // Recorded-data-only limitation is always present and listed first.
        limitations.Insert(0, new AgronomistLimitationDto
        {
            Code = LimitRecordedDataOnly,
            Message = "The assistant uses only information recorded in SABZ"
                + (context.WeatherIncluded ? ", plus external weather data" : string.Empty)
                + ". It provides information only and performs no actions in SABZ."
        });

        var contextBlock = await BuildContextBlockAsync(context, activeCrops, question, ct);
        return (context, limitations, contextBlock);
    }

    private async Task<string> BuildContextBlockAsync(
        AgronomistFarmContextDto context,
        List<Crop> activeCrops,
        string question,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FARM CONTEXT RECORDED IN SABZ:");
        sb.AppendLine($"Farm name: {context.FarmName}");
        sb.AppendLine($"Location: {context.Tehsil}, {context.District}, {context.Province}");
        sb.AppendLine($"Farm size: {context.FarmSize} {context.FarmSizeUnit}");
        sb.AppendLine($"Soil type: {context.SoilType ?? "not recorded"}");
        sb.AppendLine($"Irrigation type: {context.IrrigationType ?? "not recorded"}");

        if (context.ActiveCrops.Count == 0)
        {
            sb.AppendLine("Active crops: none recorded in SABZ.");
        }
        else
        {
            sb.AppendLine("Active crops:");
            foreach (var crop in context.ActiveCrops)
            {
                var stage = string.IsNullOrWhiteSpace(crop.GrowthStage) ? "stage not recorded" : $"stage {crop.GrowthStage}";
                sb.AppendLine($"- {crop.CropName} ({crop.Season}, {stage})");
            }
        }

        if (context.WeatherIncluded && context.WeatherSummary is not null)
            sb.AppendLine($"EXTERNAL WEATHER (not recorded in SABZ): {context.WeatherSummary}");

        // Curated SABZ disease reference only when the question is disease-related.
        if (IsDiseaseRelated(question))
        {
            var diseaseNames = await GetDiseaseReferenceAsync(activeCrops, ct);
            if (diseaseNames.Count > 0)
                sb.AppendLine($"SABZ CURATED DISEASE REFERENCE (possible known issues): {string.Join("; ", diseaseNames)}");
        }

        return sb.ToString();
    }

    private async Task<List<string>> GetDiseaseReferenceAsync(List<Crop> activeCrops, CancellationToken ct)
    {
        var catalogCrop = activeCrops.FirstOrDefault(c => c.CropCatalogId.HasValue);
        var catalogId = catalogCrop?.CropCatalogId;

        var diseases = await _diseaseInformation.GetActiveForCropAsync(catalogId, ct);
        return diseases
            .Select(d => d.DiseaseName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(_settings.MaxDiseaseReferencesInContext)
            .ToList();
    }

    private static bool IsDiseaseRelated(string question)
        => DiseaseKeywords.Any(keyword =>
            question.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------
    //  Weather summary (external data, clearly labelled)
    // ------------------------------------------------------------------

    private static string? SummarizeWeather(WeatherResponseDto weather)
    {
        var current = weather.Current;
        if (current is null)
            return null;

        var parts = new List<string>();
        if (current.Temperature.HasValue) parts.Add($"temperature {current.Temperature.Value:F1} C");
        if (current.RelativeHumidity.HasValue) parts.Add($"humidity {current.RelativeHumidity.Value:F0}%");
        if (current.WindSpeed.HasValue) parts.Add($"wind {current.WindSpeed.Value:F0} km/h");
        if (current.Precipitation.HasValue && current.Precipitation.Value > 0)
            parts.Add($"precipitation {current.Precipitation.Value:F1} mm");

        if (parts.Count == 0)
            return null;

        var source = string.IsNullOrWhiteSpace(weather.Source) ? "weather provider" : weather.Source;
        return $"{string.Join(", ", parts)} (external data from {source}).";
    }

    // ------------------------------------------------------------------
    //  Language detection (English / Urdu only - the supported set)
    // ------------------------------------------------------------------

    private static string DetectLanguage(string question)
    {
        foreach (var ch in question)
        {
            // Arabic script (Urdu) + Arabic supplement ranges.
            if (ch >= 0x0600 && ch <= 0x06FF) return "ur";
            if (ch >= 0x0750 && ch <= 0x077F) return "ur";
        }
        return "en";
    }

    // ------------------------------------------------------------------
    //  Prompts (AI response rules baked in; system prompt is never exposed)
    // ------------------------------------------------------------------

    private static string BuildSystemPrompt(string language)
    {
        var languageName = language == "ur" ? "Urdu" : "English";

        return
            "You are the SABZ AI Agronomist, a helpful and careful agricultural assistant for farmers in Pakistan.\n" +
            $"Respond in {languageName}.\n" +
            "Follow these rules strictly:\n" +
            "- Answer agriculture and farming questions using practical, clear, easy-to-understand language.\n" +
            "- Use the provided SABZ farm context only when it is relevant to the question.\n" +
            "- If information is missing, say you do not have enough recorded information. Do not guess or invent details.\n" +
            "- Clearly state uncertainty where appropriate, and ask the farmer for more information when needed.\n" +
            "- NEVER claim to have physically inspected the farm.\n" +
            "- NEVER claim certainty about a crop disease from a text description alone. You may give general possibilities " +
            "and suggest using SABZ's photo-based disease detection feature for better evidence.\n" +
            "- NEVER invent crops, farm conditions, disease results, weather observations, prices, local regulations, or government programs.\n" +
            "- NEVER say that you created, updated, or deleted any farm, crop, financial transaction, monitoring check, " +
            "notification, or user record. You provide information only and perform no actions in SABZ.\n" +
            "- Distinguish clearly between information recorded in SABZ, information the farmer provides, and external weather data.";
    }

    private static string BuildUserPrompt(string question, string contextBlock)
    {
        return
            $"{contextBlock}\n" +
            "FARMER'S QUESTION:\n" +
            $"{question}\n\n" +
            "Please answer the farmer's question, following all of your rules.";
    }
}
