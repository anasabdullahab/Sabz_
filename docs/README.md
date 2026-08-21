# SABZ Backend Documentation

This folder documents the SABZ agricultural backend, feature by feature,
in the order the features were built.

## Document index

| Document | Feature |
| --- | --- |
| [architecture.md](architecture.md) | Solution layout, clean-architecture rules, cross-cutting concerns |
| [prompt-1-authentication.md](prompt-1-authentication.md) | User registration, login, JWT authentication |
| [prompt-2-location-and-farms.md](prompt-2-location-and-farms.md) | Pakistan administrative data (Province/District/Tehsil) and farm CRUD |
| [prompt-2.1-crops.md](prompt-2.1-crops.md) | Crop catalog and per-farm crop records |
| [prompt-3-weather-intelligence.md](prompt-3-weather-intelligence.md) | Weather foundation (Open-Meteo, caching, forecast) |
| [prompt-4-crop-suitability.md](prompt-4-crop-suitability.md) | Crop suitability evaluation and recommendation foundation |
| [prompt-5-crop-recommendation.md](prompt-5-crop-recommendation.md) | Dynamic next-crop recommendation & crop history foundation |
| [pakistan-admin-data.md](pakistan-admin-data.md) | Pakistan administrative divisions dataset reference |

## API base

- Development server: `http://localhost:5073`
- Swagger UI: `http://localhost:5073/swagger`
- All farm/weather/crop-suitability/crop-recommendation endpoints require a JWT
  bearer token obtained from `POST /api/auth/login`.
