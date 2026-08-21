using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SABZ.Application.Interfaces;
using SABZ.Application.Services.Auth;
using SABZ.Application.Services.Crops;
using SABZ.Application.Services.CropRecommendation;
using SABZ.Application.Services.CropSuitability;
using SABZ.Application.Services.Farms;
using SABZ.Application.Services.Weather;
using SABZ.Infrastructure.Persistence;
using SABZ.Infrastructure.Repositories;
using SABZ.Infrastructure.Services;
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

        return services;
    }
}
