using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SABZ.Application.DTOs.Weather;
using SABZ.Application.Interfaces;
using SABZ.Domain.Exceptions;

namespace SABZ.Infrastructure.Services.Weather;

/// <summary>
/// Weather provider backed by the free Open-Meteo Forecast API.
/// No API key or signup is required (non-commercial free tier).
/// Required attribution: "Weather data by Open-Meteo.com".
/// </summary>
public class OpenMeteoWeatherProvider : IWeatherProvider
{
    private const string ForecastPath = "/v1/forecast";

    private const string CurrentVariables =
        "temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,rain," +
        "cloud_cover,wind_speed_10m,wind_direction_10m,wind_gusts_10m,weather_code,is_day";

    private const string DailyVariables =
        "temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max," +
        "rain_sum,wind_speed_10m_max,weather_code,et0_fao_evapotranspiration,sunrise,sunset";

    // Soil variables are only available with hourly resolution on Open-Meteo;
    // they are averaged per calendar day when mapping the forecast.
    private const string HourlySoilVariables = "soil_temperature_0_to_7cm,soil_moisture_0_to_7cm";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string HttpClientName = "OpenMeteo";

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoWeatherProvider> _logger;

    public OpenMeteoWeatherProvider(IHttpClientFactory httpClientFactory, ILogger<OpenMeteoWeatherProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _logger = logger;
    }

    public string SourceName => "Open-Meteo";

    public async Task<CurrentWeatherDto> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken ct)
    {
        var url = $"{ForecastPath}?latitude={Format(latitude)}&longitude={Format(longitude)}" +
                  $"&current={CurrentVariables}" +
                  "&temperature_unit=celsius&wind_speed_unit=kmh&precipitation_unit=mm&timezone=auto";

        var response = await GetAsync<OpenMeteoResponse>(url, ct);

        if (response.Current is null)
            throw new WeatherProviderException("The weather provider returned no current weather data.");

        return MapCurrent(response.Current);
    }

    public async Task<ForecastDto> GetForecastAsync(double latitude, double longitude, int days, CancellationToken ct)
    {
        var url = $"{ForecastPath}?latitude={Format(latitude)}&longitude={Format(longitude)}" +
                  $"&daily={DailyVariables}&hourly={HourlySoilVariables}&forecast_days={days}" +
                  "&temperature_unit=celsius&wind_speed_unit=kmh&precipitation_unit=mm&timezone=auto";

        var response = await GetAsync<OpenMeteoResponse>(url, ct);

        if (response.Daily is null || response.Daily.Time is null || response.Daily.Time.Length == 0)
            throw new WeatherProviderException("The weather provider returned no forecast data.");

        return MapForecast(response);
    }

    // ------------------------------------------------------------------
    //  HTTP + error handling
    // ------------------------------------------------------------------

    private async Task<T> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.GetAsync(url, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled the request – propagate as-is.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new WeatherProviderException("The weather provider request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new WeatherProviderException("Unable to reach the weather provider.", ex);
        }

        using (httpResponse)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Open-Meteo returned HTTP {Status} for {Url}.",
                    (int)httpResponse.StatusCode, url);

                if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new WeatherProviderException("The weather provider rate limit was reached. Please try again shortly.");

                throw new WeatherProviderException(
                    $"The weather provider returned an error (HTTP {(int)httpResponse.StatusCode}).");
            }

            try
            {
                var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
                var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
                return result ?? throw new WeatherProviderException("The weather provider returned an empty response.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException ex)
            {
                throw new WeatherProviderException("The weather provider returned an unexpected response format.", ex);
            }
        }
    }

    // ------------------------------------------------------------------
    //  Mapping: Open-Meteo JSON → SABZ contracts
    // ------------------------------------------------------------------

    private static CurrentWeatherDto MapCurrent(OpenMeteoCurrent c)
    {
        return new CurrentWeatherDto
        {
            Temperature = c.Temperature2m,
            ApparentTemperature = c.ApparentTemperature,
            RelativeHumidity = c.RelativeHumidity2m,
            Precipitation = c.Precipitation,
            Rain = c.Rain,
            WindSpeed = c.WindSpeed10m,
            WindDirection = c.WindDirection10m,
            WindGusts = c.WindGusts10m,
            CloudCover = c.CloudCover,
            WeatherCode = c.WeatherCode,
            IsDay = c.IsDay == 1,
            ObservationTime = ParseLocalTime(c.Time)
        };
    }

    private static ForecastDto MapForecast(OpenMeteoResponse response)
    {
        var daily = response.Daily!;
        var forecast = new ForecastDto { Timezone = response.Timezone };

        var soil = response.Hourly is null
            ? null
            : AverageSoilByDay(response.Hourly.Time, response.Hourly.SoilTemperature0To7cm, response.Hourly.SoilMoisture0To7cm);

        for (var i = 0; i < daily.Time!.Length; i++)
        {
            if (!DateOnly.TryParse(daily.Time[i], CultureInfo.InvariantCulture, out var date))
                continue;

            double? soilTemperature = null;
            double? soilMoisture = null;
            if (soil is not null && soil.TryGetValue(date, out var soilValues))
            {
                soilTemperature = soilValues.Temperature;
                soilMoisture = soilValues.Moisture;
            }

            forecast.Days.Add(new DailyForecastDto
            {
                Date = date,
                TempMin = At(daily.Temperature2mMin, i),
                TempMax = At(daily.Temperature2mMax, i),
                Precipitation = At(daily.PrecipitationSum, i),
                PrecipitationProbability = At(daily.PrecipitationProbabilityMax, i),
                Rain = At(daily.RainSum, i),
                WindSpeed = At(daily.WindSpeed10mMax, i),
                WeatherCode = At(daily.WeatherCode, i),
                Et0 = At(daily.Et0FaoEvapotranspiration, i),
                Sunrise = At(daily.Sunrise, i),
                Sunset = At(daily.Sunset, i),
                SoilTemperature = soilTemperature,
                SoilMoisture = soilMoisture
            });
        }

        return forecast;
    }

    /// <summary>
    /// Groups hourly soil readings by calendar day and averages them, since
    /// Open-Meteo exposes soil variables with hourly resolution only.
    /// </summary>
    private static Dictionary<DateOnly, (double? Temperature, double? Moisture)> AverageSoilByDay(
        string[]? hourlyTimes, double?[]? temperatures, double?[]? moistures)
    {
        var byDay = new Dictionary<DateOnly, (List<double> Temps, List<double> Moistures)>();

        if (hourlyTimes is not null)
        {
            for (var i = 0; i < hourlyTimes.Length; i++)
            {
                if (!DateTime.TryParse(hourlyTimes[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    continue;

                var day = DateOnly.FromDateTime(dt);
                if (!byDay.TryGetValue(day, out var bucket))
                {
                    bucket = (new List<double>(), new List<double>());
                    byDay[day] = bucket;
                }

                if (temperatures is not null && i < temperatures.Length && temperatures[i] is double temp)
                    bucket.Temps.Add(temp);
                if (moistures is not null && i < moistures.Length && moistures[i] is double moisture)
                    bucket.Moistures.Add(moisture);
            }
        }

        return byDay.ToDictionary(
            kv => kv.Key,
            kv => (
                kv.Value.Temps.Count > 0 ? kv.Value.Temps.Average() : (double?)null,
                kv.Value.Moistures.Count > 0 ? kv.Value.Moistures.Average() : (double?)null));
    }

    private static T? At<T>(T?[]? array, int index) where T : struct
        => array is not null && index < array.Length ? array[index] : null;

    private static string? At(string[]? array, int index)
        => array is not null && index < array.Length ? array[index] : null;

    private static DateTime? ParseLocalTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;

    private static string Format(double coordinate)
        => coordinate.ToString("0.######", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------
    //  Open-Meteo response shapes (internal only – never exposed by SABZ)
    // ------------------------------------------------------------------

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
        [JsonPropertyName("current")] public OpenMeteoCurrent? Current { get; set; }
        [JsonPropertyName("daily")] public OpenMeteoDaily? Daily { get; set; }
        [JsonPropertyName("hourly")] public OpenMeteoHourly? Hourly { get; set; }
    }

    private sealed class OpenMeteoCurrent
    {
        [JsonPropertyName("time")] public string? Time { get; set; }
        [JsonPropertyName("temperature_2m")] public double? Temperature2m { get; set; }
        [JsonPropertyName("relative_humidity_2m")] public double? RelativeHumidity2m { get; set; }
        [JsonPropertyName("apparent_temperature")] public double? ApparentTemperature { get; set; }
        [JsonPropertyName("is_day")] public int? IsDay { get; set; }
        [JsonPropertyName("precipitation")] public double? Precipitation { get; set; }
        [JsonPropertyName("rain")] public double? Rain { get; set; }
        [JsonPropertyName("cloud_cover")] public double? CloudCover { get; set; }
        [JsonPropertyName("wind_speed_10m")] public double? WindSpeed10m { get; set; }
        [JsonPropertyName("wind_direction_10m")] public double? WindDirection10m { get; set; }
        [JsonPropertyName("wind_gusts_10m")] public double? WindGusts10m { get; set; }
        [JsonPropertyName("weather_code")] public int? WeatherCode { get; set; }
    }

    private sealed class OpenMeteoDaily
    {
        [JsonPropertyName("time")] public string[]? Time { get; set; }
        [JsonPropertyName("temperature_2m_max")] public double?[]? Temperature2mMax { get; set; }
        [JsonPropertyName("temperature_2m_min")] public double?[]? Temperature2mMin { get; set; }
        [JsonPropertyName("precipitation_sum")] public double?[]? PrecipitationSum { get; set; }
        [JsonPropertyName("precipitation_probability_max")] public double?[]? PrecipitationProbabilityMax { get; set; }
        [JsonPropertyName("rain_sum")] public double?[]? RainSum { get; set; }
        [JsonPropertyName("wind_speed_10m_max")] public double?[]? WindSpeed10mMax { get; set; }
        [JsonPropertyName("weather_code")] public int?[]? WeatherCode { get; set; }
        [JsonPropertyName("et0_fao_evapotranspiration")] public double?[]? Et0FaoEvapotranspiration { get; set; }
        [JsonPropertyName("sunrise")] public string[]? Sunrise { get; set; }
        [JsonPropertyName("sunset")] public string[]? Sunset { get; set; }
    }

    private sealed class OpenMeteoHourly
    {
        [JsonPropertyName("time")] public string[]? Time { get; set; }
        [JsonPropertyName("soil_temperature_0_to_7cm")] public double?[]? SoilTemperature0To7cm { get; set; }
        [JsonPropertyName("soil_moisture_0_to_7cm")] public double?[]? SoilMoisture0To7cm { get; set; }
    }
}
