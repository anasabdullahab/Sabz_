using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SABZ.Application.DTOs.Weather;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Weather;

/// <summary>
/// Application-level weather service.
/// Validates farm ownership and coordinates, applies in-memory caching,
/// delegates to the configured IWeatherProvider and returns clean SABZ responses.
/// </summary>
public class WeatherService : IWeatherService
{
    private readonly IFarmRepository _farmRepository;
    private readonly IWeatherProvider _weatherProvider;
    private readonly IMemoryCache _cache;
    private readonly WeatherSettings _settings;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(
        IFarmRepository farmRepository,
        IWeatherProvider weatherProvider,
        IMemoryCache cache,
        IOptions<WeatherSettings> settings,
        ILogger<WeatherService> logger)
    {
        _farmRepository = farmRepository;
        _weatherProvider = weatherProvider;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<WeatherResponseDto> GetCurrentWeatherAsync(Guid userId, Guid farmId, CancellationToken ct)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId);
        var (lat, lon) = GetValidatedCoordinates(farm);

        var cacheKey = BuildCacheKey("current", lat, lon);
        if (!_cache.TryGetValue<CurrentWeatherDto>(cacheKey, out var current) || current is null)
        {
            current = await _weatherProvider.GetCurrentWeatherAsync(lat, lon, ct);
            _cache.Set(cacheKey, current, TimeSpan.FromMinutes(_settings.CurrentCacheMinutes));
            _logger.LogInformation(
                "Fetched current weather from {Source} for ({Latitude}, {Longitude}).",
                _weatherProvider.SourceName, lat, lon);
        }

        return BuildResponse(farm, lat, lon, current: current);
    }

    public async Task<WeatherResponseDto> GetForecastAsync(Guid userId, Guid farmId, CancellationToken ct)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId);
        var (lat, lon) = GetValidatedCoordinates(farm);

        var cacheKey = BuildCacheKey("forecast", lat, lon);
        if (!_cache.TryGetValue<ForecastDto>(cacheKey, out var forecast) || forecast is null)
        {
            forecast = await _weatherProvider.GetForecastAsync(lat, lon, _settings.ForecastDays, ct);
            _cache.Set(cacheKey, forecast, TimeSpan.FromMinutes(_settings.ForecastCacheMinutes));
            _logger.LogInformation(
                "Fetched {Days}-day forecast from {Source} for ({Latitude}, {Longitude}).",
                _settings.ForecastDays, _weatherProvider.SourceName, lat, lon);
        }

        return BuildResponse(farm, lat, lon, forecast: forecast);
    }

    // ------------------------------------------------------------------
    //  Ownership + validation
    // ------------------------------------------------------------------

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return farm;
    }

    private static (double Latitude, double Longitude) GetValidatedCoordinates(Farm farm)
    {
        if (farm.Latitude is null || farm.Longitude is null)
        {
            throw new ValidationException(
                "GPS coordinates are required for precise farm weather. " +
                "Please update the farm with its latitude and longitude first.");
        }

        var lat = (double)farm.Latitude.Value;
        var lon = (double)farm.Longitude.Value;

        var errors = new Dictionary<string, string[]>();
        if (lat is < -90 or > 90)
            errors["Latitude"] = ["Latitude must be between -90 and 90."];
        if (lon is < -180 or > 180)
            errors["Longitude"] = ["Longitude must be between -180 and 180."];

        if (errors.Count > 0)
            throw new ValidationException(errors);

        return (lat, lon);
    }

    // ------------------------------------------------------------------
    //  Caching + response mapping
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds a cache key from rounded coordinates so that tiny GPS
    /// differences do not create a cache entry per request.
    /// </summary>
    private string BuildCacheKey(string type, double latitude, double longitude)
    {
        var precision = _settings.CoordinatePrecision;
        return $"weather:{type}:{Math.Round(latitude, precision)}:{Math.Round(longitude, precision)}";
    }

    private WeatherResponseDto BuildResponse(
        Farm farm,
        double latitude,
        double longitude,
        CurrentWeatherDto? current = null,
        ForecastDto? forecast = null)
    {
        return new WeatherResponseDto
        {
            FarmId = farm.Id,
            Latitude = latitude,
            Longitude = longitude,
            Source = _weatherProvider.SourceName,
            RetrievedAt = DateTime.UtcNow,
            Current = current,
            Forecast = forecast
        };
    }
}
