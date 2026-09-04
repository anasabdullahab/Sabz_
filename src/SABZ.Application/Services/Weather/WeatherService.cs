using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// Coordinate resolution order:
///   1. Override coordinates (from device "Locate Me")
///   2. Farm GPS (manually saved)
///   3. Tehsil centre (automatic fallback from location seed data)
/// On provider failure, serves stale cached data with a warning.
/// </summary>
public class WeatherService : IWeatherService
{
    private readonly IFarmRepository _farmRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IWeatherProvider _weatherProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly WeatherSettings _settings;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(
        IFarmRepository farmRepository,
        ILocationRepository locationRepository,
        IWeatherProvider weatherProvider,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<WeatherSettings> settings,
        ILogger<WeatherService> logger)
    {
        _farmRepository = farmRepository;
        _locationRepository = locationRepository;
        _weatherProvider = weatherProvider;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<WeatherResponseDto> GetCurrentWeatherAsync(
        Guid userId, Guid farmId, double? overrideLat, double? overrideLon, CancellationToken ct)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId);
        var resolved = await ResolveCoordinatesAsync(farm, overrideLat, overrideLon);

        var cacheKey = BuildCacheKey("current", resolved.Lat, resolved.Lon);
        CurrentWeatherDto? current = null;
        bool isStale = false;
        string? staleWarning = null;

        if (!_cache.TryGetValue<CurrentWeatherDto>(cacheKey, out current) || current is null)
        {
            try
            {
                current = await _weatherProvider.GetCurrentWeatherAsync(resolved.Lat, resolved.Lon, ct);
                _cache.Set(cacheKey, current, TimeSpan.FromMinutes(_settings.CurrentCacheMinutes));
                _logger.LogInformation(
                    "Fetched current weather from {Source} for ({Latitude}, {Longitude}).",
                    _weatherProvider.SourceName, resolved.Lat, resolved.Lon);
            }
            catch (WeatherProviderException ex)
            {
                // Serve stale cached data if available
                if (_cache.TryGetValue<CurrentWeatherDto>(cacheKey, out var stale) && stale is not null)
                {
                    current = stale;
                    isStale = true;
                    staleWarning = "Showing previous weather data. Please check your internet connection for the latest update.";
                    _logger.LogWarning(ex, "Weather provider failed; serving stale cache.");
                }
                else
                {
                    throw; // No cached data at all — propagate the error
                }
            }
        }

        return BuildResponse(farm, resolved, current: current, isStale: isStale, staleWarning: staleWarning);
    }

    public async Task<WeatherResponseDto> GetForecastAsync(
        Guid userId, Guid farmId, double? overrideLat, double? overrideLon, CancellationToken ct)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId);
        var resolved = await ResolveCoordinatesAsync(farm, overrideLat, overrideLon);

        var cacheKey = BuildCacheKey("forecast", resolved.Lat, resolved.Lon);
        ForecastDto? forecast = null;
        bool isStale = false;
        string? staleWarning = null;

        if (!_cache.TryGetValue<ForecastDto>(cacheKey, out forecast) || forecast is null)
        {
            try
            {
                forecast = await _weatherProvider.GetForecastAsync(resolved.Lat, resolved.Lon, _settings.ForecastDays, ct);
                _cache.Set(cacheKey, forecast, TimeSpan.FromMinutes(_settings.ForecastCacheMinutes));
                _logger.LogInformation(
                    "Fetched {Days}-day forecast from {Source} for ({Latitude}, {Longitude}).",
                    _settings.ForecastDays, _weatherProvider.SourceName, resolved.Lat, resolved.Lon);
            }
            catch (WeatherProviderException ex)
            {
                if (_cache.TryGetValue<ForecastDto>(cacheKey, out var stale) && stale is not null)
                {
                    forecast = stale;
                    isStale = true;
                    staleWarning = "Showing previous forecast data. Please check your internet connection for the latest update.";
                    _logger.LogWarning(ex, "Weather provider failed; serving stale cache.");
                }
                else
                {
                    throw;
                }
            }
        }

        return BuildResponse(farm, resolved, forecast: forecast, isStale: isStale, staleWarning: staleWarning);
    }

    /// <inheritdoc />
    /// <summary>
    /// Tehsil-based preview used by the dashboard onboarding (no farm required).
    /// Shares the same coordinate-keyed cache as farm weather so a preview and a
    /// farm lookup for the same tehsil hit the provider only once.
    /// </summary>
    public async Task<WeatherPreviewDto> GetPreviewAsync(int tehsilId, CancellationToken ct)
    {
        var tehsil = await _locationRepository.GetTehsilByIdAsync(tehsilId)
            ?? throw new NotFoundException("Tehsil not found.");

        if (tehsil.Latitude is null || tehsil.Longitude is null)
            throw new ValidationException("Weather data is not available for this tehsil yet. Please pick another tehsil.");

        var lat = tehsil.Latitude.Value;
        var lon = tehsil.Longitude.Value;

        var currentKey = BuildCacheKey("current", lat, lon);
        if (!_cache.TryGetValue<CurrentWeatherDto>(currentKey, out var current) || current is null)
        {
            current = await _weatherProvider.GetCurrentWeatherAsync(lat, lon, ct);
            _cache.Set(currentKey, current, TimeSpan.FromMinutes(_settings.CurrentCacheMinutes));
        }

        var forecastKey = BuildCacheKey("forecast", lat, lon);
        if (!_cache.TryGetValue<ForecastDto>(forecastKey, out var forecast) || forecast is null)
        {
            forecast = await _weatherProvider.GetForecastAsync(lat, lon, _settings.ForecastDays, ct);
            _cache.Set(forecastKey, forecast, TimeSpan.FromMinutes(_settings.ForecastCacheMinutes));
        }

        _logger.LogInformation(
            "Served weather preview for tehsil {Tehsil} ({Latitude}, {Longitude}) from {Source}.",
            tehsil.Name, lat, lon, _weatherProvider.SourceName);

        return new WeatherPreviewDto
        {
            LocationName = tehsil.Name,
            Latitude = lat,
            Longitude = lon,
            Source = _weatherProvider.SourceName,
            RetrievedAt = DateTime.UtcNow,
            Current = current,
            Forecast = forecast,
        };
    }

    public async Task<ReverseGeocodeDto> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct)
    {
        var url = $"https://geocoding-api.open-meteo.com/v1/reverse?latitude={latitude.ToString("0.######", CultureInfo.InvariantCulture)}" +
                  $"&longitude={longitude.ToString("0.######", CultureInfo.InvariantCulture)}&count=1&language=en&format=json";

        try
        {
            var client = _httpClientFactory.CreateClient("OpenMeteo");
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return new ReverseGeocodeDto { Name = "Unknown", DisplayLabel = "Unknown location" };

            var stream = await response.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<GeocodingResponse>(stream, _jsonOptions, ct);

            if (result?.Results is null || result.Results.Length == 0)
                return new ReverseGeocodeDto { Name = "Unknown", DisplayLabel = "Unknown location" };

            var place = result.Results[0];
            var label = place.Admin1 is not null ? $"{place.Name}, {place.Admin1}" : place.Name ?? "Unknown";

            return new ReverseGeocodeDto
            {
                Name = place.Name ?? "Unknown",
                Admin1 = place.Admin1,
                Country = place.Country,
                DisplayLabel = label
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reverse geocoding failed for ({Lat}, {Lon}).", latitude, longitude);
            return new ReverseGeocodeDto { Name = "Unknown", DisplayLabel = "Unknown location" };
        }
    }

    // ------------------------------------------------------------------
    //  Ownership + coordinate resolution
    // ------------------------------------------------------------------

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId)
    {
        var farm = await _farmRepository.GetByIdAsync(farmId)
            ?? throw new NotFoundException("Farm not found.");

        if (farm.UserId != userId)
            throw new ForbiddenException("You do not have access to this farm.");

        return farm;
    }

    /// <summary>
    /// Resolves coordinates with priorityised source tracking:
    /// 1. Device override (from browser geolocation — most precise)
    /// 2. Farm GPS (manually saved by farmer)
    /// 3. Tehsil centre (automatic fallback from location seed data)
    /// </summary>
    private async Task<(double Lat, double Lon, string Source, string? LocationName)> ResolveCoordinatesAsync(
        Farm farm, double? overrideLat, double? overrideLon)
    {
        // 1. Device override (Locate Me)
        if (overrideLat.HasValue && overrideLon.HasValue)
        {
            ValidateCoordinateRange(overrideLat.Value, overrideLon.Value);
            var locationName = await BuildLocationNameFromFarm(farm);
            return (overrideLat.Value, overrideLon.Value, "DeviceGps", locationName);
        }

        // 2. Farm GPS
        if (farm.Latitude.HasValue && farm.Longitude.HasValue)
        {
            var lat = (double)farm.Latitude.Value;
            var lon = (double)farm.Longitude.Value;
            ValidateCoordinateRange(lat, lon);
            return (lat, lon, "FarmGps", null);
        }

        // 3. Tehsil centre fallback
        var tehsil = await _locationRepository.GetTehsilByIdAsync(farm.TehsilId);
        if (tehsil?.Latitude.HasValue == true && tehsil?.Longitude.HasValue == true)
        {
            return ((double)tehsil!.Latitude!.Value, (double)tehsil.Longitude!.Value,
                    "TehsilCentre", $"{tehsil.Name} (approximate)");
        }

        throw new ValidationException(
            "GPS coordinates are required for weather data. " +
            "Please use the \"Locate Me\" button or select your Tehsil.");
    }

    private static void ValidateCoordinateRange(double lat, double lon)
    {
        var errors = new Dictionary<string, string[]>();
        if (lat is < -90 or > 90)
            errors["Latitude"] = ["Latitude must be between -90 and 90."];
        if (lon is < -180 or > 180)
            errors["Longitude"] = ["Longitude must be between -180 and 180."];
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private async Task<string?> BuildLocationNameFromFarm(Farm farm)
    {
        // Build a label from the farm's known location hierarchy
        try
        {
            var tehsil = await _locationRepository.GetTehsilByIdAsync(farm.TehsilId);
            if (tehsil is not null) return tehsil.Name;
        }
        catch { /* non-critical */ }
        return null;
    }

    // ------------------------------------------------------------------
    //  Caching + response mapping
    // ------------------------------------------------------------------

    private string BuildCacheKey(string type, double latitude, double longitude)
    {
        var precision = _settings.CoordinatePrecision;
        return $"weather:{type}:{Math.Round(latitude, precision)}:{Math.Round(longitude, precision)}";
    }

    private WeatherResponseDto BuildResponse(
        Farm farm,
        (double Lat, double Lon, string Source, string? LocationName) resolved,
        CurrentWeatherDto? current = null,
        ForecastDto? forecast = null,
        bool isStale = false,
        string? staleWarning = null)
    {
        return new WeatherResponseDto
        {
            FarmId = farm.Id,
            Latitude = resolved.Lat,
            Longitude = resolved.Lon,
            CoordinateSource = resolved.Source,
            LocationName = resolved.LocationName,
            Source = _weatherProvider.SourceName,
            RetrievedAt = DateTime.UtcNow,
            IsStale = isStale,
            StaleWarning = staleWarning,
            Current = current,
            Forecast = forecast
        };
    }

    // ------------------------------------------------------------------
    //  Reverse geocoding response shape
    // ------------------------------------------------------------------

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GeocodingResponse
    {
        [JsonPropertyName("results")] public GeocodingPlace[]? Results { get; set; }
    }

    private sealed class GeocodingPlace
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("admin1")] public string? Admin1 { get; set; }
        [JsonPropertyName("country")] public string? Country { get; set; }
    }
}
