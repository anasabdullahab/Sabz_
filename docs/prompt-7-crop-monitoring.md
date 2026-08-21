# Prompt 7 — Smart Crop Monitoring Schedule & Farmer Reminder Foundation

## Purpose

Prompt 7 gives every crop a **data-driven monitoring schedule**: SABZ knows,
from reference rules in the database, *when* a farmer should walk the field
and *what* to inspect after planting. Farmers record what they saw
(an observation, never a diagnosis) or explicitly skip a check.

```
MONITORING RULE (DB) → CHECKS GENERATED AT CROP CREATION →
DUE / UPCOMING LISTS → FARMER COMPLETES OR SKIPS → OBSERVATION STORED
```

Hard rules enforced by design:

- Rules come from the `CropMonitoringRules` reference table — **no switch
  statements on crop names**, adding a rule needs no code change.
- Checks are **never auto-completed**. Only the farmer can complete or skip.
- An observation (`Normal` / `SomethingSuspicious`) records what the farmer
  reported — it is **not a disease diagnosis**.
- `SomethingSuspicious` only **recommends** the existing Prompt 6 photo
  workflow (`photoAnalysisRecommended = true`). Prompt 7 **never calls the AI
  provider** and needs no API key.
- No notifications, no satellite, no weather-event automation are built.
  The data model is designed so a future notification service can simply
  query due/completed/skipped checks.

Prompts 1–6 endpoints are **unchanged** (verified by regression tests).

## Data model (migration `AddCropMonitoring`, additive + reversible)

| Table | Purpose |
| --- | --- |
| `CropMonitoringRules` | Reference rules: crop (nullable = all crops), day offset after planting, title/description/inspection items, priority, `TriggerType`, `IsActive`, source label |
| `CropMonitoringChecks` | One check per (crop, rule): scheduled date, persisted status, rule content **snapshot**, observation, notes, timestamps, denormalised `FarmId` for cheap user-scoped queries |

- Persisted status: `Scheduled` / `Completed` / `Skipped`.
- Farmer-facing status is computed against a centralised UTC clock
  (`ISystemClock`): `ScheduledDate <= UtcNow` → **Due**, otherwise
  **Upcoming**. Nothing is ever auto-transitioned, and a skipped check can
  never reappear as due.
- Idempotency: unique index on `(CropId, RuleId)` (where `RuleId` is not
  null) + a service-level pre-check of existing rule ids.
- Checks snapshot the rule's title/description/items/priority at generation
  time, so later rule edits never rewrite a farmer's history.
- Deleting a crop cascade-deletes its checks. `TriggerType` ("Scheduled"
  today) leaves room for future weather/satellite/manual triggers without a
  schema change.
- `Down()` drops both tables; all Prompt 1–6 tables and data are untouched
  (verified row-count before/after).

### Seeded rules (15, honest foundation dataset)

Wheat, Rice, Cotton, Potato and Tomato get 3 scheduled checks each
(offsets such as 14/30/60 days after planting), using only real
`CropCatalog` ids. Content is cautious general agronomy, labelled
"Initial SABZ monitoring reference dataset (general agronomic knowledge,
expert review recommended)". Crops without rules (e.g. Sugarcane) simply
get no checks and an honest message.

## Check generation

- **Automatic**: creating a crop with a planting date generates its checks
  immediately. The hook is wrapped in graceful degradation — crop creation
  can never fail because of monitoring.
- **On demand / backfill**: `POST /api/crops/{cropId}/monitoring/generate`
  works for crops created before Prompt 7 or without a planting date.
- **Idempotent**: a second run creates nothing and reports
  "Monitoring checks already exist for this crop; nothing was duplicated."
- No planting date → no checks + explanatory note. No applicable rules →
  no checks + "No active monitoring rules are available for this crop yet."
- Scheduled date = `PlantingDate.Date + DayOffsetAfterPlanting` (UTC).

## Endpoints (all JWT-authenticated; userId always from the token)

| Method & route | Purpose |
| --- | --- |
| `GET /api/crops/{cropId}/monitoring` | All checks of one crop (ownership via user → farm → crop) |
| `POST /api/crops/{cropId}/monitoring/generate` | Idempotent check generation (backfill-safe) |
| `GET /api/monitoring/due` | The user's due checks, most overdue first |
| `GET /api/monitoring/upcoming` | The user's upcoming checks, soonest first |
| `POST /api/monitoring/{checkId}/complete` | Complete with `observation` (`Normal` / `SomethingSuspicious`) + optional `notes` |
| `POST /api/monitoring/{checkId}/skip` | Skip (optional `notes`) |

Status semantics: `401` unauthenticated, `404` unknown crop/check,
`403` foreign crop/check, `400` invalid observation or notes > 1000 chars,
`409` duplicate/illegal transitions (complete-after-complete,
complete-after-skip, skip-after-complete, skip-after-skip).

### Completion response

```json
{
  "check": { "id": "...", "status": "Completed", "observation": "SomethingSuspicious",
             "photoAnalysisRecommended": true, "...": "..." },
  "photoAnalysisRecommended": true,
  "nextAction": "Upload a clear photo of the affected crop or leaf for AI analysis using the crop's disease-detection endpoint.",
  "observationNote": "This observation records what the farmer reported during the monitoring check. It is not a disease diagnosis."
}
```

`Normal` → `photoAnalysisRecommended = false` and "continue with the next
scheduled monitoring check". The Prompt 6 endpoint is **referenced, never
invoked** — no AI call, no API key required for Prompt 7.

## Security & time handling

- `[Authorize]` everywhere; ownership always derived from the JWT user via
  user → farm → crop → check. No user ids in request bodies.
- All scheduling/comparisons use `ISystemClock.UtcNow` (single injectable
  clock, registered as a singleton) — consistent UTC, unit-testable.
- Notes are capped at 1000 characters and trimmed; observation values are
  parsed strictly (`Enum.TryParse`, case-insensitive).

## Testing

`test-monitoring.ps1` (local-only, idempotent, 92 checks, **92/92 passed**,
verified green on consecutive reruns) covers:

- Automatic generation on crop creation (3 wheat checks at offsets
  14/30/60, rule content snapshot, future-dated crop all Upcoming,
  no-planting-date crop creates nothing, no-rules crop creates nothing).
- Idempotent generation endpoint (double-run adds nothing; honest notes for
  missing planting date / missing rules).
- Due/upcoming correctness (UTC-computed, sorted, user-scoped isolation).
- Auth/ownership (401 no token, 404 unknown crop/check, 403 foreign
  crop/check on every endpoint).
- Completion: Normal vs SomethingSuspicious (photo recommendation only),
  invalid/empty observation → 400, oversized notes → 400, duplicate
  complete → 409, skip-after-complete / complete-after-skip / skip-after-skip
  → 409, case-insensitive observation.
- Final-state consistency: completed/skipped never due, skipped absent from
  upcoming, per-crop history readable.
- Regressions: Prompt 6 (502 not-configured, no fake AI result, non-image
  400), Prompt 4 suitability, Prompt 5 recommendations, crop CRUD with
  check cascade on delete.
- Data guards: Ahmed Farm snapshot untouched, 15 seed rules intact, crop
  catalog intact, no orphaned checks after cleanup.

The script requires **no DashScope key and no live AI inference**; it
removes its own fixture crops (checks cascade-delete) so reruns start clean.

## What was NOT built (explicit scope)

- No notification delivery (no email/SMS/push, no notification tables).
- No satellite integration, no weather-event triggers, no fake alerts.
- No AI calls of any kind (Prompt 6 is reused by reference only).
- No farm-location redesign and no changes to Prompts 1–6 behaviour.

## Components

| Layer | Component |
| --- | --- |
| Domain | `CropMonitoringRule`, `CropMonitoringCheck`, `MonitoringCheckStatus`, `MonitoringObservation` |
| Application | `IMonitoringService`, `MonitoringService`, `ICropMonitoringRuleRepository`, `ICropMonitoringCheckRepository`, `ISystemClock`, monitoring DTOs |
| Infrastructure | `CropMonitoringRuleRepository`, `CropMonitoringCheckRepository`, `SystemClock`, DbContext configuration, 15 seed rules, DI registrations |
| API | `MonitoringController` (6 endpoints), crop-creation hook in `CropService` with graceful degradation |

Migration: `20260821154034_AddCropMonitoring` (additive: 2 tables, 15 seed
rows, 5 indexes; `Down()` drops both tables; zero changes to existing
tables/data).
