# Prompt 5 — Dynamic Next Crop Recommendation & Crop History Foundation

## Purpose

Answers the question:

> "Considering this farm's conditions and the crops previously grown on this farm,
> which crops should the farmer consider growing next?"

Prompt 4 answers *"how suitable is this crop for this farm?"* — Prompt 5 builds on
top of it: *"given farm suitability AND crop history, which suitable crops should
the farmer consider growing next?"*

## Relationship to Prompt 4

- Prompt 5 **reuses** `ICropSuitabilityService.EvaluateAsync` for farm ownership,
  season validation/auto-detection, weather retrieval and all suitability scoring.
  Nothing from the Prompt 4 engine was duplicated or modified
  (one additive field, `CandidateCategory`, was added to the suitability result DTO).
- `GET /api/farms/{farmId}/crop-suitability` is unchanged.
- Weather is retrieved by the Prompt 4 evaluation only — at most once per request;
  Prompt 5 adds no weather calls, providers or caches.

## API endpoint

```
GET /api/farms/{farmId}/crop-recommendations?season={Rabi|Kharif}
```

- JWT bearer authentication (`[Authorize]`); user id from JWT claim only.
- Farm ownership enforced by the reused suitability evaluation (403 on foreign farm,
  404 unknown farm, 400 invalid season, 401 missing/invalid token).
- `season` optional; auto-detected from the current month when omitted
  (Kharif = months 4–9, configurable).

## Response design (DTOs)

`CropRecommendationResponseDto`:

| Field | Meaning |
| --- | --- |
| `farmId`, `location` | Farm identity and province/district/tehsil names (from Prompt 4) |
| `evaluationSeason`, `seasonSource` | Season used; `ClientProvided` or `AutoDetected` |
| `evaluatedAt` | UTC timestamp |
| `cropHistory` | `CropHistorySummaryDto` — what could be determined from actual records |
| `recommendations` | `CropRecommendationItemDto[]`, ordered best-first |
| `disclaimer` | Not a guaranteed agricultural outcome |

`CropHistorySummaryDto`: `available`, `previousCropName`, `previousCropCategory`,
`previousCropSeason`, `usableRecordCount`, `historyNote`.

`CropRecommendationItemDto`: `cropId`, `cropName`, `farmSuitability` (Prompt 4
category), `recommendation` (farmer-facing category), `suitabilityScore` (internal
Prompt 4 score, exposed for transparency — not a probability),
`historyConsideration` (`Positive`/`Caution`/`Negative` or null), `explanation`,
`positiveFactors`, `limitations`, `missingData`.

EF entities are never exposed directly.

## Crop history model

The **existing `Crop` entity is the farm-crop record** (there is no separate
FarmCrop table — deliberately not created). Prompt 5 extended it minimally:

- **Added:** `HarvestDate` (`datetime2 NULL`) — completes the crop lifecycle
  (planted → growing → harvested), enables reliable completed-cycle detection.
- Existing fields reused: `CropCatalogId`, `CropName`, `Season`, `PlantingDate`,
  `GrowthStage`, `PreviousCrop` (free text — not trusted when real records exist),
  `Status`, `FarmId`.

`HarvestDate` was also plumbed through `CreateCropDto`, `UpdateCropDto`,
`CropResponseDto` and `CropService` (create/update/map).

## Previous crop determination (documented rule)

Implemented in `CropRecommendationService.DeterminePreviousCrop`:

1. Load the farm's crop records once (`ICropRepository.GetHistoryByFarmIdAsync`,
   includes `CropCatalog`, excludes `Status = "Planned"`, ordered by
   `PlantingDate ?? CreatedAt` descending).
2. The **previous crop is the most recent COMPLETED crop cycle**: the newest record
   with status `Harvested` or `Failed`.
3. `Status = "Active"` records are the *current* crop and never count as previous.
4. If no completed record exists, history is reported **unavailable** — a previous
   crop is never invented, and the free-text `PreviousCrop` field is never used as
   a substitute.

With multiple historical records, the recency ordering above selects the most
relevant one deterministically. Future phases can extend this to multi-season /
multi-year windows without schema changes.

## Crop-change data model

`CropChangeRule` (table `CropChangeRules`) — small, data-driven, independent of
controller code:

| Column | Meaning |
| --- | --- |
| `PreviousCategory` / `NextCategory` | `CropCatalog.Category` of previous / candidate crop |
| `Effect` | `Positive`, `Caution` or `Negative` |
| `Explanation` | Farmer-friendly reason text |
| `IsActive` | Disabled rules are ignored |
| `Source` | Data provenance |

Rules are keyed by **catalog category**, not individual crops — `CropCatalog.Category`
was inspected first and is safe for this use (stable seeded reference values:
Cereal, Pulse, Vegetable, Oilseed, …). Unique index on
(`PreviousCategory`, `NextCategory`). Crop-specific rules can be added later.

### Initial seed data (6 rules)

Clearly labelled *"Initial SABZ crop-change reference dataset (general agronomic
knowledge, expert review recommended)"* — NOT a complete scientific model:

| Previous → Next | Effect |
| --- | --- |
| Pulse → Cereal | Positive |
| Cereal → Pulse | Positive |
| Oilseed → Cereal | Positive |
| Cereal → Cereal | Caution |
| Vegetable → Vegetable | Caution |
| Pulse → Pulse | Caution |

No `Negative` rules are seeded: "Not Recommended" is only ever produced when a
data-backed rule exists.

## Recommendation logic

1. Prompt 4 suitability category maps 1:1 to a starting level:
   Highly Suitable = 3, Suitable = 2, Moderately Suitable = 1, Low Suitability = 0.
2. If history is available AND both categories are known AND an active rule matches
   (case-insensitive): `Caution` subtracts `CautionLevelAdjustment` (default 1),
   `Negative` subtracts `NegativeLevelAdjustment` (default 2). `Positive` and
   no-rule cases keep the level. Result is clamped to 0–3.
3. Level → farmer-facing category (single centralized array in
   `CropRecommendationService`; no magic thresholds scattered elsewhere):
   3 = Highly Recommended, 2 = Recommended, 1 = Consider, 0 = Not Recommended.
4. **Candidates are never removed** because of history concerns — they are
   down-ranked with an explanation. Poor farm suitability can never be rescued by
   history (adjustments only subtract).
5. Sorting: recommendation category desc → suitability score desc → crop name asc.

No second independent 0–100 agricultural score was introduced.

## Farmer-facing explanations

Composed from actually evaluated factors, e.g.:

- *"Your farm conditions are highly suitable for gram (chickpea), and the available
  crop-history information supports considering it as your next crop."*
- *"Your farm conditions are highly suitable for wheat, but the available
  crop-history information suggests considering other options."*
- No history: *"...Crop-history information is not available, so this recommendation
  is based on the available farm suitability information."*
- No rule: *"...crop-change information is not available for {previous crop} followed
  by this crop, so the recommendation is based on farm suitability."*

Rule explanations are also appended to `positiveFactors` (Positive) or
`limitations` (Caution/Negative) prefixed with "Crop history:". Missing crop-change
data is reported in `missingData` and never forces "Not Recommended".

## Missing-data handling

| Missing | Behavior |
| --- | --- |
| No completed crop records | `cropHistory.available=false`, recommendation from suitability only, never invented |
| Previous crop without catalog link/category | History reported, but crop-change not evaluated; reported in `missingData` per candidate |
| No rule for the category pair | Level unchanged; reported in `missingData` per candidate |
| No GPS / weather outage | Prompt 4 behavior unchanged: climate score 0, listed in `missingData`, no crash |

## Services / interfaces / repositories

| Component | Location |
| --- | --- |
| `ICropRecommendationService` / `CropRecommendationService` | `SABZ.Application/Interfaces`, `Services/CropRecommendation` |
| `CropRecommendationSettings` (`CropRecommendation` section) | `SABZ.Application/Interfaces` |
| `ICropChangeRuleRepository` / `CropChangeRuleRepository` | Application interface, `SABZ.Infrastructure/Repositories` |
| `ICropRepository.GetHistoryByFarmIdAsync` | Added to existing repository |
| `CropRecommendationController` | `SABZ.API/Controllers` |

DI registrations in `SABZ.Infrastructure/DependencyInjection.cs` (options binding +
2 scoped services). Config section in `appsettings.json` / `appsettings.template.json`:

```json
"CropRecommendation": { "CautionLevelAdjustment": 1, "NegativeLevelAdjustment": 2 }
```

## Performance

Per recommendation request: exactly 1 suitability evaluation (which itself performs
2 reference-data queries + at most 1 weather fetch), 1 crop-history query, 1 rules
query — all reused across every candidate crop. `AsNoTracking` on rules. No N+1.

## Security

Same SABZ ownership pattern as all farm endpoints: JWT → `ClaimTypes.NameIdentifier`
→ farm ownership check inside the reused suitability evaluation. No client-supplied
user id is accepted.

## Database changes

Migration **`20260821122810_AddCropRecommendationFoundation`**:

- `ALTER TABLE Crops ADD HarvestDate datetime2 NULL` (purely additive, no data loss)
- `CREATE TABLE CropChangeRules` + unique index + 6 seed rows

Verified after apply: CropCatalog 22, CropRequirements 9, RegionalCropSuitabilities 44,
Provinces 7, Farms/Crops counts unchanged.

## Testing

`test-crop-recommendation.ps1` (idempotent, 43 checks) — **43/43 PASS**. Covers
spec tests 1–12: valid farm with history, no-history farm, multiple-history rule,
Rabi/Kharif/invalid season, missing coordinates, 403/404/401, ranking order and
consistency, Prompt 4 suitability regression, weather current/forecast/cache
regression, Crop CRUD regression (incl. `HarvestDate` round-trip), and the
Ahmed Farm integrity guard. Not committed (contains local test credentials).

## Known limitations / NOT implemented

- Crop-change data is category-level only (no crop-specific rules) and covers 6 pairs.
- No `Negative` rules seeded yet.
- History window is "most recent completed cycle" only — no multi-season/multi-year
  sequencing yet (architecture allows it).
- Free-text `PreviousCrop` remains on the entity for backward compatibility but is
  ignored by the recommendation engine when records exist.
- No frontend. No AI agronomist, chatbot, disease/pest, irrigation/fertilizer,
  market or financial features.

## Future extension points

- Populate `CropChangeRules` from PARC / extension / expert-reviewed sources.
- Crop-specific rules (extend matching before category fallback).
- Multi-season history weighting, rotation planning across upcoming seasons.
- Surface `HarvestDate` transitions in a dedicated "complete harvest" endpoint.
