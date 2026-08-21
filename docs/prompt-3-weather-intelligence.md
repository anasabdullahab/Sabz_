# SABZ — Weather Intelligence Foundation

## Overview

SABZ retrieves weather information for a farmer's farm using its GPS
coordinates. The weather system is a reusable backend capability that future
features (crop recommendation, risk analysis, irrigation intelligence, etc.)
will consume.

## Weather Provider: Open-Meteo

- **Official website:** <https://open-meteo.com/>
- **API used:** Open-Meteo Forecast API (<https://open-meteo.com/en/docs>)

### Free API characteristics

- No API key required
- No signup required
- The free tier is intended for **non-commercial use**
- Usage limits apply

### Required attribution

Weather responses include a `source` field identifying the provider.
Frontends must display appropriate attribution:

> Weather data by Open-Meteo.com

Open-Meteo is **not** a government weather service. SABZ does not generate
raw weather observations itself; all weather values come from the provider's
numerical weather models.

## Architecture

```
WeatherController (API)
        │
IWeatherService (Application — ownership, validation, caching)
        │
IWeatherProvider (Application — provider abstraction)
        │
OpenMeteoWeatherProvider (Infrastructure — HTTP + JSON mapping)
        │
Open-Meteo Forecast API (external)
```

The provider is replaceable: any future provider can implement
`IWeatherProvider` without changing controllers or business logic.

## API Endpoints

Both endpoints require JWT authentication, and the authenticated user must
own the farm.

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/farms/{farmId}/weather/current` | Current weather at the farm coordinates |
| GET | `/api/farms/{farmId}/weather/forecast` | 7-day daily forecast at the farm coordinates |

### Units

| Quantity | Unit |
|----------|------|
| Temperature | °C (Celsius) |
| Wind speed | km/h |
| Precipitation / rain / ET0 | mm |
| Humidity / cloud cover | % |
| Soil moisture | m³/m³ |

### Variables provided

**Current weather:** temperature, apparent temperature, relative humidity,
precipitation, rain, wind speed/direction/gusts, cloud cover, WMO weather
code, day/night flag.

**Daily forecast:** min/max temperature, precipitation sum, precipitation
probability, rain sum, max wind speed, weather code, reference
evapotranspiration (ET0), sunrise, sunset, soil temperature (0–7 cm),
soil moisture (0–7 cm).

Note: Open-Meteo exposes soil variables with hourly resolution only; the
provider requests them hourly and reports the per-day average in the daily
forecast.

## Coordinate Requirements

- Weather uses `Farm.Latitude` / `Farm.Longitude`.
- Valid ranges: latitude −90..90, longitude −180..180.
- If a farm has no coordinates, the API returns a clear validation message.
  Coordinates are never invented.

## Caching

Weather responses are cached in-memory (`IMemoryCache`) to avoid repeated
external calls for the same location:

| Data | Cache key pattern | Default duration |
|------|-------------------|------------------|
| Current weather | `weather:current:{lat}:{lon}` | 15 minutes |
| Forecast | `weather:forecast:{lat}:{lon}` | 60 minutes |

Coordinates are rounded to 2 decimal places (~1.1 km) in cache keys so tiny
GPS differences do not create excessive cache entries. Durations are
configurable (see below).

## Configuration

`appsettings.json` section `Weather`:

```json
"Weather": {
    "BaseUrl": "https://api.open-meteo.com",
    "TimeoutSeconds": 15,
    "CurrentCacheMinutes": 15,
    "ForecastCacheMinutes": 60,
    "ForecastDays": 7,
    "CoordinatePrecision": 2
}
```

## Error Handling

External failures (network errors, timeouts, HTTP errors, rate limiting,
malformed responses) are translated into clean SABZ error responses —
typically HTTP 502 with the message "Weather data is currently unavailable"
or a specific provider message. Raw HttpClient exceptions and stack traces
are never exposed to clients.
