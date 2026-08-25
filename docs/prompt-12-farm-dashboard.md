# Prompt 12: Unified Farm Dashboard & Insights

## Purpose

A single, **read-only**, farm-level overview that combines the information SABZ
already records and calculates into one unified view: farm details, crop
summaries, Prompt 7 monitoring, Prompt 8 notifications, the Prompt 9 financial
ledger, Prompt 10 financial health, Prompt 11 performance and (when available)
the external Prompt 3 weather. It gives a farmer one place to see "what SABZ
knows about this farm" without duplicating any existing calculation.

> **The SABZ Farm Dashboard combines information already recorded or calculated
> within SABZ into a unified view. It does not independently verify real-world
> farm activity, predict future outcomes, determine farming skill,
> creditworthiness, or financial eligibility.**

**The dashboard is an aggregation/orchestration layer, never a new source of
truth.** Every value is retrieved or computed at request time from the existing
services; no business logic is re-implemented, no derived value is persisted,
and no AI is used.

## Hard rules honoured

- **No new tables, no migration, no schema change.** The dashboard reads the
  existing `Farms`, `Crops`, `FinancialTransactions`, `CropMonitoringChecks`
  and `Notifications` stores; `TableCount` stays 16 before and after and
  `dotnet ef migrations has-pending-model-changes` reports no changes.
- **Nothing derived is persisted.** No dashboard result, total, score, cache
  entry or history row is stored — the whole response is assembled per request.
- **Reuse, never duplicate.** Each section delegates to the service that owns
  that logic (`IFinancialService`, `IFinancialHealthService`,
  `IFarmPerformanceService`, `IMonitoringService`, `INotificationService`,
  `IWeatherService`); no calculation is copied into the dashboard.
- **Read-only.** The dashboard never changes monitoring state, never creates or
  marks notifications, and never writes any record.
- **Monitoring/notification behaviour preserved.** Due checks are read through
  the existing Prompt 7 read path, so the Prompt 8 lazy/idempotent
  `MonitoringDue` reminder behaviour is kept exactly — the dashboard adds no
  second notification mechanism and generates no duplicates.
- **No loan/credit/banking/insurance/investment/payment/budget/forecasting
  logic.** Financial health is shown only as the existing Prompt 10 indicator +
  recorded-data completeness (never reinterpreted as creditworthiness or loan
  eligibility), and performance uses factual wording ("best recorded crop",
  never "most profitable").
- Ownership is derived `JWT user -> Farm`; the client never supplies a
  `UserId`/`OwnerId` and none is ever exposed.
- Money stays `decimal` end-to-end.

## Endpoint (`[Authorize]`, user-scoped GET)

| Route | Response |
| --- | --- |
| `GET /api/farms/{farmId}/dashboard` | `FarmDashboardDto` — unified view over all sections below |

### Response sections

| Section | Content | Source reused |
| --- | --- | --- |
| `farm` | `farmId`, `farmName`, province/district/tehsil, size + unit, soil, irrigation, `hasCoordinates` (never `userId`) | `IFarmRepository` |
| `crops` | `totalCrops`, `activeCrops`, per-crop `cropId`/`cropName`/`season`/`growthStage`/`status` (no invented health/yield) | `ICropRepository` |
| `monitoring` | `dueChecks`, `upcomingChecks`, `completedChecks`, `skippedChecks`, `totalChecks` | `IMonitoringService` + `ICropMonitoringCheckRepository` |
| `notifications` | `unreadCount` + bounded `recentNotifications` (newest first, user-scoped) | `INotificationService` |
| `financial` | `totalIncome`, `totalExpenses`, `netResult`, `transactionCount` (decimal) | `IFinancialService` (Prompt 9) |
| `financialHealth` | `healthIndicator`, `healthExplanation`, `completenessStatus`, `completenessScore`, `disclaimer` | `IFinancialHealthService` (Prompt 10) |
| `performance` | `overallStatus`, `statusExplanation`, `netResult`, `bestRecordedCrop`, `weakestRecordedCrop` | `IFarmPerformanceService` (Prompt 11) |
| `weather` | external current weather + `note`, or `null` | `IWeatherService` (Prompt 3) |
| `limitations` | structured `{ code, message }` data-context facts | dashboard |
| `disclaimer` / `generatedAt` | mandatory factual disclaimer + UTC assembly time | dashboard + `ISystemClock` |

## Weather decision

Weather **is included**, but only when it can be reused cleanly:

- Reuses the existing `IWeatherService.GetCurrentWeatherAsync` — no new
  provider, no duplicated weather logic.
- Only attempted when the farm has GPS coordinates; otherwise `weather` is
  `null` and a `NoCoordinates` limitation is emitted.
- A weather failure can **never** break the dashboard: the call is wrapped, a
  failure logs a warning, sets `weather = null` and adds a
  `WeatherUnavailable` limitation, and the rest of the dashboard still
  returns.
- External weather is always clearly distinguished from farmer-recorded SABZ
  data via a `note`: *"Weather is external data from the configured weather
  provider. It is not recorded by the farmer in SABZ."*

## Monitoring & notification behaviour

- Due/upcoming counts are obtained from the existing user-scoped read paths and
  filtered to the requested farm — the dashboard never mutates check status.
- Reading due checks goes through the same Prompt 7 path the API already uses,
  so the Prompt 8 lazy/idempotent `MonitoringDue` generation is preserved
  exactly. Reading the dashboard repeatedly never adds duplicate notifications.
- Completed/skipped/total counts reuse the Prompt 11
  `ICropMonitoringCheckRepository.GetFarmCheckEventsAsync` projection.

## Structured limitations

`limitations` is a list of `{ code, message }` pairs; codes are stable and
factual:

| Code | Emitted when |
| --- | --- |
| `RecordedDataOnly` | always (first) — states the view reflects only SABZ-recorded/calculated data |
| `NoCrops` | the farm has no crop records |
| `NoFinancialTransactions` | the farm has no financial transactions |
| `NoCoordinates` | the farm has no GPS coordinates (weather not shown) |
| `WeatherUnavailable` | coordinates exist but the weather retrieval failed |

Every response also carries the mandatory `disclaimer` quoted at the top of
this document.

## Security

Mirrors the existing farm endpoints: `401` without/with an invalid token,
`404` unknown farm, `403` another user's farm. The response exposes
`farmId`/`cropId` but never a user id, and `UserId` is never accepted from the
client.

## Performance approach

- Pure orchestration over existing services — no N+1 introduced by the
  dashboard itself; each underlying service already aggregates SQL-side with
  `AsNoTracking` and bounded lists (recent notifications capped at 5).
- No circular dependencies: the dashboard depends on the feature services, and
  no feature service depends on the dashboard.
- UTC time comes from `ISystemClock`; no `DateTime.Now`/`UtcNow` is used.

## Components

| Component | Location |
| --- | --- |
| `IFarmDashboardService` | `src/SABZ.Application/Interfaces` |
| `FarmDashboardDto` + section/limitation DTOs | `src/SABZ.Application/DTOs/Dashboard` |
| `FarmDashboardService` | `src/SABZ.Application/Services/Dashboard` |
| `FarmDashboardController` | `src/SABZ.API/Controllers` |
| DI registration | `src/SABZ.Infrastructure/DependencyInjection.cs` |

## Testing

`test-farm-dashboard.ps1` (untracked, idempotent, self-cleaning): auth 401
checks, ownership 404/403, no-`userId`/`ownerId` leakage, farm/crop sections,
monitoring counts cross-checked against the source endpoints plus no-state-
change and no-duplicate-notification assertions, user-scoped bounded
notifications, financial/health/performance sections cross-checked against the
Prompt 9/10/11 endpoints, weather inclusion/failure tolerance and the external-
data note, structured limitations and the mandatory disclaimer, wording
guardrails, and cleanup/integrity checks. The suite is run twice consecutively
to prove idempotency.

## Deliberately NOT built

- Prompt 13 or any further feature.
- AI recommendations/advice, predictions, forecasting or yield prediction.
- Loans, credit scoring/approval, banking, insurance, investments, payments or
  any external financial provider.
- Background jobs, schedulers (Hangfire/Quartz/cron), queues, SMS/email/
  WhatsApp/push/Firebase delivery.
- New notification generation, new disease detection, satellite/NDVI.
- Duplicate services or any persisted dashboard data/cache/history.
