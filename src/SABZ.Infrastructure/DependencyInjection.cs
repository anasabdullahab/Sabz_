using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SABZ.Application.Interfaces;
using SABZ.Application.Services.Agronomist;
using SABZ.Application.Services.Auth;
using SABZ.Application.Services.Community;
using SABZ.Application.Services.Crops;
using SABZ.Application.Services.CropRecommendation;
using SABZ.Application.Services.CropSuitability;
using SABZ.Application.Services.Dashboard;
using SABZ.Application.Services.DiseaseDetection;
using SABZ.Application.Services.Farms;
using SABZ.Application.Services.Financial;
using SABZ.Application.Services.Monitoring;
using SABZ.Application.Services.Notifications;
using SABZ.Application.Services.Performance;
using SABZ.Application.Services.Weather;
using SABZ.Infrastructure.Persistence;
using SABZ.Infrastructure.Repositories;
using SABZ.Infrastructure.Services;
using SABZ.Infrastructure.Services.Agronomist;
using SABZ.Infrastructure.Services.DiseaseDetection;
using SABZ.Infrastructure.Services.Weather;

namespace SABZ.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SabzDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Auth
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Locations
        services.AddScoped<ILocationRepository, LocationRepository>();

        // Farms
        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<IFarmService, FarmService>();

        // Crops
        services.AddScoped<ICropRepository, CropRepository>();
        services.AddScoped<ICropService, CropService>();

        // Crop monitoring (Prompt 7) - rules, scheduled checks, centralised UTC clock
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<ICropMonitoringRuleRepository, CropMonitoringRuleRepository>();
        services.AddScoped<ICropMonitoringCheckRepository, CropMonitoringCheckRepository>();
        services.AddScoped<IMonitoringService, MonitoringService>();

        // In-app notifications (Prompt 8) - central reminder foundation (in-app only,
        // no SMS/email/push); due notifications are generated lazily from the
        // monitoring read path, never by background jobs
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();

        // Farm P&L financial ledger (Prompt 9) - farmer-entered income/expenses,
        // dynamically computed summaries, decimal money, JWT-derived ownership
        services.AddScoped<IFinancialTransactionRepository, FinancialTransactionRepository>();
        services.AddScoped<IFinancialService, FinancialService>();

        // Financial health & readiness intelligence (Prompt 10) - read-only
        // analytics derived from the Prompt 9 ledger; no persisted scores,
        // no AI, no background jobs, no new tables
        services.AddScoped<IFinancialHealthService, FinancialHealthService>();

        // Farm performance dashboard & decision intelligence (Prompt 11) -
        // read-only intelligence derived from crops, the Prompt 9 ledger and
        // Prompt 7 monitoring checks; nothing derived is persisted, no AI,
        // no background jobs, no new tables
        services.AddScoped<IFarmPerformanceService, FarmPerformanceService>();

        // Unified farm dashboard & insights (Prompt 12) - read-only
        // aggregation/orchestration over existing features (farms, crops,
        // Prompt 7 monitoring, Prompt 8 notifications, Prompt 9 ledger,
        // Prompt 10 health, Prompt 11 performance, Prompt 3 weather); nothing
        // derived is persisted, no new tables, no background jobs, no AI
        services.AddScoped<IFarmDashboardService, FarmDashboardService>();

        // Weather configuration and caching
        services.Configure<WeatherSettings>(configuration.GetSection(WeatherSettings.SectionName));
        services.AddMemoryCache();
        services.AddHttpClient("OpenMeteo", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<WeatherSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        // Weather - provider implemented in Infrastructure, service implemented in Application
        services.AddScoped<IWeatherProvider, OpenMeteoWeatherProvider>();
        services.AddScoped<IWeatherService, WeatherService>();

        // Crop suitability - configuration, data repository and scoring service
        services.Configure<CropSuitabilitySettings>(configuration.GetSection(CropSuitabilitySettings.SectionName));
        services.AddScoped<ICropSuitabilityDataRepository, CropSuitabilityDataRepository>();
        services.AddScoped<ICropSuitabilityService, CropSuitabilityService>();

        // Crop recommendation (Prompt 5) - reuses suitability + weather, adds crop-history guidance
        services.Configure<CropRecommendationSettings>(configuration.GetSection(CropRecommendationSettings.SectionName));
        services.AddScoped<ICropChangeRuleRepository, CropChangeRuleRepository>();
        services.AddScoped<ICropRecommendationService, CropRecommendationService>();

        // Disease detection (Prompt 6) - image validation, AI vision provider, curated advice
        services.Configure<DiseaseDetectionSettings>(configuration.GetSection(DiseaseDetectionSettings.SectionName));
        services.AddHttpClient(QwenVisionDiseaseDetectionProvider.HttpClientName, (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<DiseaseDetectionSettings>>().Value;
            client.BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        });
        services.AddScoped<IImageValidator, SharpImageValidator>();
        services.AddScoped<IPlantDiseaseDetectionProvider, QwenVisionDiseaseDetectionProvider>();
        services.AddScoped<IDiseaseInformationRepository, DiseaseInformationRepository>();
        services.AddScoped<IDiseaseDetectionService, DiseaseDetectionService>();

        // Voice-first AI agronomist assistant (Prompt 13) - information & guidance
        // only, strictly read-only. Reuses the shared DashScope connection already
        // configured for disease detection (same ApiBaseUrl + ApiKey), so no second
        // API key or HTTP stack is introduced. No persisted chat history, no
        // background jobs, no new tables.
        services.Configure<AgronomistSettings>(configuration.GetSection(AgronomistSettings.SectionName));
        services.AddHttpClient(QwenAgronomistAiProvider.HttpClientName, (sp, client) =>
        {
            var connection = sp.GetRequiredService<IOptions<DiseaseDetectionSettings>>().Value;
            client.BaseAddress = new Uri(connection.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(connection.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(connection.ApiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
        });
        services.AddScoped<IAgronomistAiProvider, QwenAgronomistAiProvider>();
        services.AddScoped<ISpeechToTextProvider, QwenSpeechToTextProvider>();
        services.AddScoped<IAgronomistAssistantService, AgronomistAssistantService>();

        // Farmer community foundation (Prompt 14) - agriculture-focused posts
        // and comments for authenticated farmers. Soft-deleted rows stay
        // hidden from all normal queries; reads are DB-side paginated SQL
        // projections. No notifications, no AI moderation, no background jobs.
        services.AddScoped<ICommunityPostRepository, CommunityPostRepository>();
        services.AddScoped<ICommunityCommentRepository, CommunityCommentRepository>();
        services.AddScoped<ICommunityService, CommunityService>();

        return services;
    }
}
