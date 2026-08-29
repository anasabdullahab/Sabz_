# SABZ Backend Architecture

## Solution layout (Clean Architecture)

The solution has four projects; there is no `.sln` file, builds run against
the API project which references everything else:

```
src/
  SABZ.Domain          Entities, domain exceptions. No dependencies.
  SABZ.Application     DTOs, interfaces, services (business logic). Depends on Domain.
  SABZ.Infrastructure  EF Core DbContext, migrations, seed data, repositories,
                       external providers (Open-Meteo). Implements Application interfaces.
  SABZ.API             ASP.NET Core Web API, controllers, middleware, configuration.
```

Dependency direction is strictly inward: API -> Infrastructure -> Application -> Domain.

## Key conventions

- **Services in Application, providers in Infrastructure.** Business logic
  (auth, farms, crops, weather service, crop suitability) lives in
  `SABZ.Application/Services/*`; anything touching EF Core, files, or external
  HTTP APIs lives in Infrastructure.
- **Ownership checks are server-side.** User identity comes from the JWT claim
  `ClaimTypes.NameIdentifier` only; clients never send a user id in request bodies.
- **Domain exceptions -> HTTP status codes** via centralized exception
  handling middleware:
  - `ValidationException` -> 400
  - `AuthenticationException` -> 401
  - `ForbiddenException` -> 403
  - `NotFoundException` -> 404
  - `ConflictException` -> 409
  - `WeatherProviderException` -> 502
  - `DiseaseProviderException` -> 502
- **Configuration via `IOptions<T>`** bound from `appsettings.json` sections
  (`Jwt`, `Weather`, `CropSuitability`, `CropRecommendation`, `DiseaseDetection`) and registered in
  `SABZ.Infrastructure/DependencyInjection.cs`.
- **Database:** SQL Server LocalDB (`(localdb)\mssqllocaldb`, database `SabzDB`),
  EF Core migrations, `HasData` seeding for reference datasets, runtime seeding
  of administrative divisions by `LocationDataSeeder` from an embedded JSON resource.

## Cross-cutting capabilities

| Capability | Implementation |
| --- | --- |
| Authentication | JWT bearer (`SABZ.Application/Services/Auth`) |
| Weather data | Open-Meteo via named HttpClient + `IMemoryCache` (15 min current / 60 min forecast) |
| Crop suitability | Data-driven scoring engine over `CropRequirement` + `RegionalCropSuitability` reference data |
| Crop recommendation | Reuses suitability engine + crop history + `CropChangeRule` reference data (Prompt 5) |
| Disease detection | AI vision pipeline (image validation → plant relevance → disease → advice) via `IPlantDiseaseDetectionProvider` + curated `DiseaseInformation` data (Prompt 6) |
| Crop monitoring | Data-driven schedule from `CropMonitoringRule` reference data; checks generated per crop, computed Due/Upcoming against `ISystemClock` UTC; farmer observations recommend (never invoke) the Prompt 6 photo workflow (Prompt 7) |
| In-app notifications | Central `Notification` store (in-app only, no external delivery); idempotent `MonitoringDue` reminders generated lazily from the monitoring due read path with app-level pre-check + unique index (Prompt 8) |
| Financial ledger | Farmer-entered income/expense `FinancialTransaction` rows (no UserId column, ownership via farm); P&L summaries computed dynamically, never persisted; decimal(18,2) money (Prompt 9) |
| Financial health & readiness | Read-only aggregates over the Prompt 9 ledger (health indicator, category breakdown, monthly activity, recorded-data completeness 0-100); SQL-side, never persisted, no new tables, no AI; not a loan/credit/banking/insurance/investment decision (Prompt 10) |
| Farm performance dashboard | Read-only intelligence over crops, the Prompt 9 ledger and Prompt 7 monitoring checks (overview, per-crop breakdown, recorded activity in SABZ); deterministic statuses/rankings, structured limitations, SQL-side, never persisted, no new tables, no AI; never a measure of real-world performance or creditworthiness (Prompt 11) |
| Unified farm dashboard | Read-only aggregation/orchestration over existing features (farm, crops, Prompt 7 monitoring, Prompt 8 notifications, Prompt 9 ledger, Prompt 10 health, Prompt 11 performance, Prompt 3 weather) behind `GET /api/farms/{farmId}/dashboard`; reuses each owning service, persists nothing, adds no tables/migrations/jobs/AI, preserves Prompt 8 lazy reminders, and never claims real-world activity, skill, outcomes or creditworthiness (Prompt 12) |
| Voice-first AI agronomist | Farm-aware assistant behind `POST /api/farms/{farmId}/agronomist/chat` and `/voice`; text or in-memory audio transcription (`ISpeechToTextProvider`) answered by `IAgronomistAiProvider`, both reusing the shared Prompt 6 DashScope connection (no duplicated keys/HTTP clients); focused read-only farm context (profile + bounded active crops + optional external weather + curated disease names), English/Urdu, structured limitations + mandatory disclaimer; strictly read-only — no writes, no chat history, no new tables/migrations/jobs (Prompt 13) |
| Farmer community | Persistent `CommunityPost`/`CommunityComment` tables behind `/api/community/**`; authenticated farmers post agricultural content and discuss via comments; DB-side paginated SQL projections (author display name + comment counts computed in SQL, no N+1), deterministic ordering (posts `CreatedAt DESC, Id DESC`, comments `CreatedAt ASC, Id ASC`), owner-only soft delete (post deletion also hides its comments), safe HTTP/HTTPS-only image URL reference (no binary, no filesystem paths), JWT-derived ownership (`IsOwnedByCurrentUser`), no likes/followers/messaging, no notifications, no AI moderation (Prompt 14) |
| Error handling | Exception middleware + problem-style JSON errors |
