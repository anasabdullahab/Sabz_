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
| [prompt-6-disease-detection.md](prompt-6-disease-detection.md) | AI crop disease identification & agricultural advice foundation |
| [prompt-7-crop-monitoring.md](prompt-7-crop-monitoring.md) | Smart crop monitoring schedule & farmer reminder foundation |
| [prompt-8-notifications.md](prompt-8-notifications.md) | Central in-app notification & reminder foundation |
| [prompt-9-financial.md](prompt-9-financial.md) | Farm profit & loss (P&L) financial ledger foundation |
| [prompt-10-financial-health.md](prompt-10-financial-health.md) | Farm financial health & readiness intelligence (read-only) |
| [prompt-11-farm-performance.md](prompt-11-farm-performance.md) | Farm performance dashboard & decision intelligence (read-only) |
| [prompt-12-farm-dashboard.md](prompt-12-farm-dashboard.md) | Unified farm dashboard & insights (read-only aggregation) |
| [prompt-13-voice-agronomist.md](prompt-13-voice-agronomist.md) | Voice-first AI agronomist assistant (text + voice, read-only) |
| [prompt-14-farmer-community.md](prompt-14-farmer-community.md) | Farmer community foundation (posts + comments, soft-deleted) |
| [prompt-15-marketplace-inbox.md](prompt-15-marketplace-inbox.md) | Farmer marketplace + private inbox foundation (listings + conversations, no payments) |
| [prompt-16-input-calculator.md](prompt-16-input-calculator.md) | Precision crop input & dosage calculator (pure arithmetic, read-only) |
| [pakistan-admin-data.md](pakistan-admin-data.md) | Pakistan administrative divisions dataset reference |

## API base

- Development server: `http://localhost:5073`
- Swagger UI: `http://localhost:5073/swagger`
- All farm/weather/crop-suitability/crop-recommendation/disease-detection/
  monitoring/notification/financial/financial-health/farm-performance/
  farm-dashboard/agronomist/community/marketplace/input-calculator endpoints
  require a JWT bearer token obtained from `POST /api/auth/login`.
