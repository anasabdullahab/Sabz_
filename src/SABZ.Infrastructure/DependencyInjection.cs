using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SABZ.Application.Interfaces;
using SABZ.Application.Services.Auth;
using SABZ.Application.Services.Crops;
using SABZ.Application.Services.CropRecommendation;
using SABZ.Application.Services.CropSuitability;
using SABZ.Application.Services.DiseaseDetection;
using SABZ.Application.Services.Farms;
using SABZ.Application.Services.Financial;
using SABZ.Application.Services.Monitoring;
using SABZ.Application.Services.Notifications;
using SABZ.Application.Services.Weather;
using SABZ.Infrastructure.Persistence;
using SABZ.Infrastructure.Repositories;
using SABZ.Infrastructure.Services;
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

        return services;
    }
}
