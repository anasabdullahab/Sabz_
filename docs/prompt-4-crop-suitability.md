# PROMPT 4 — Crop Suitability & Recommendation Foundation

## 1. Purpose

Evaluate, for a given farm and season, how suitable each known crop is —
expressed as a 0–100 suitability score with per-factor breakdowns and plain
explanations. The engine is **fully data-driven**: no per-crop if/else in C#,
no hard-coded agricultural facts outside seeded reference data.

> Scores are a SABZ suitability **evaluation based on the currently available
> data model**, not a guarantee of agricultural outcomes. Expert review is
> recommended before production decisions.

## 2. Endpoint

```
GET /api/farms/{farmId}/crop-suitability?season=Rabi|Kharif
```

- JWT authentication required; the authenticated user must **own** the farm
  (otherwise 403; unknown farm -> 404; invalid season -> 400; no token -> 401).
- `season` is optional. When omitted it is **auto-detected** from the current
  date using the configured Kharif month range (default April–September =>
  Kharif, otherwise Rabi). The response states which happened
  (`seasonSource`: `ClientProvided` / `AutoDetected`).
- Implemented by a thin `CropSuitabilityController` delegating to
  `ICropSuitabilityService` (Application layer).

## 3. Response shape

```json
{
  "farmId": "...",
  "location": { "province": "Punjab", "district": "Rawalpindi", "tehsil": "Rawalpindi" },
  "evaluationSeason": "Rabi",
  "seasonSource": "ClientProvided",
  "evaluatedAt": "2026-08-21T11:20:00Z",
  "weatherDataAvailable": true,
  "crops": [
    {
      "cropCatalogId": 12,
      "cropName": "Gram (Chickpea)",
      "suitabilityScore": 85,
      "suitabilityLevel": "Highly Suitable",
      "factorScores": { "location": 20, "climate": 25, "soil": 20, "water": 15, "season": 15 },
      "positiveFactors": ["..."],
      "limitations": ["..."],
      "missingData": ["..."]
    }
  ]
}
```

- `crops` is sorted by score descending (then by name).
- Only crops that have requirement data **for the evaluated season** appear.

## 4. Data model

### CropRequirement (new)

| Column | Type | Notes |
| --- | --- | --- |
| Id | int PK identity | |
| CropCatalogId | int FK -> CropCatalog (Restrict) | |
| Season | nvarchar(50) | `Rabi` or `Kharif` |
| GrowingDurationDays | int NULL | informational |
| MinTempC / MaxTempC | decimal(5,2) NULL | suitable temperature envelope |
| WaterRequirement | nvarchar(20) | `Low` / `Medium` / `High` |
| SuitableSoils | nvarchar(500) NULL | CSV list, case-insensitive match |
| Source | nvarchar(500) | data provenance |

### RegionalCropSuitability (extended)

Extended with `int? TehsilId` (FK -> Tehsils, Restrict) + index. Rule
precedence at evaluation time: **tehsil rule > district rule > province rule**
(a province rule is a row with `DistrictId NULL`). `SuitabilityScore` is 1–10.

## 5. Scoring engine

Weights (centralized in `CropSuitabilitySettings`, bound from
`appsettings.json` -> `CropSuitability`):

| Factor | Weight | How it is evaluated |
| --- | --- | --- |
| Season | 15 | Full points: the crop has a requirement row for the evaluation season |
| Location | 25 | Best matching regional rule (tehsil > district > province): `ruleScore/10 × 25`; no rule -> 0 + limitation |
| Climate | 25 | 7-day forecast average min/max vs crop range: in-range full points, linear taper (10 °C outside -> 0); weather unavailable -> missing |
| Soil | 20 | Farm `SoilType` present in crop's CSV soils (case-insensitive); farm soil unset -> missing; mismatch -> limitation |
| Water | 15 | `High` water need requires irrigation (`Rainfed`/`None` count as no irrigation); farm irrigation unset -> missing |

- Levels (thresholds configurable): ≥80 `Highly Suitable`, ≥60 `Suitable`,
  ≥40 `Moderately Suitable`, else `Low Suitability`.
- **Missing data is never assumed perfect**: an unevaluated factor scores 0
  and is reported in `missingData`; it never crashes the evaluation.
- Each crop result explains itself: `positiveFactors`, `limitations`, `missingData`.

## 6. Weather integration

- Reuses `IWeatherService.GetForecastAsync` (the PROMPT 3 abstraction with its
  60-minute forecast cache). **At most one weather fetch per evaluation**,
  shared across all crops.
- No GPS coordinates, or provider failure, degrades gracefully:
  `weatherDataAvailable=false`, climate listed in `missingData`.
  Open-Meteo is never called directly from the suitability code.

## 7. Seed data

Migration `AddCropSuitabilityFoundation`:

- Adds `Mung bean` (id 21) and `Mash bean` (id 22) to the crop catalog.
- Seeds **9 crop requirements** (Wheat/Rabi, Rice/Kharif, Maize/Kharif,
  Cotton/Kharif, Sugarcane/Kharif, Gram/Rabi, Lentil/Rabi, Mung/Kharif,
  Mash/Kharif) sourced as *"Initial SABZ suitability dataset (general
  agronomic knowledge, expert review recommended)"*.
- Seeds **44 regional rules** with the current Pakistan Administrative
  Divisions district ids: district-level rules (e.g., Faisalabad 102,
  Sialkot 106, Multan 104, Larkana 243, Swat 228, Badin 232, Sibi 177) plus
  province-level baselines for Punjab/Sindh/KP/Balochistan so the location
  factor is meaningful everywhere. No tehsil-level rules are seeded yet.

## 8. Query efficiency

Reference data is loaded once per evaluation in exactly two queries
(`CropSuitabilityDataRepository`: requirements with catalog join, regional
rules), both `AsNoTracking`. No N+1.

## 9. Configuration (`appsettings.json`)

```json
"CropSuitability": {
  "LocationWeight": 25, "ClimateWeight": 25, "SoilWeight": 20,
  "WaterWeight": 15, "SeasonWeight": 15,
  "HighlySuitableThreshold": 80, "SuitableThreshold": 60, "ModerateThreshold": 40,
  "KharifStartMonth": 4, "KharifEndMonth": 9
}
```

## 10. Security & ownership

- `[Authorize]` on the controller; user id from `ClaimTypes.NameIdentifier`.
- Farm load verifies existence (404) and ownership (403) before any work.
- No client-supplied user ids are honored anywhere.

## 11. Error handling summary

| Case | Result |
| --- | --- |
| No/invalid token | 401 |
| Invalid `season` value | 400 with message listing supported seasons |
| Unknown farm | 404 |
| Farm owned by another user | 403 |
| Weather provider outage | 200 partial evaluation (`weatherDataAvailable=false`) |

## 12. Files added / changed

- `SABZ.Domain/Entities/CropRequirement.cs` (new)
- `SABZ.Domain/Entities/RegionalCropSuitability.cs` (+TehsilId)
- `SABZ.Infrastructure/Persistence/SabzDbContext.cs` (configuration)
- `SABZ.Application/DTOs/CropSuitability/*` (new)
- `SABZ.Application/Interfaces/ICropSuitabilityService.cs`,
  `ICropSuitabilityDataRepository.cs`, `CropSuitabilitySettings.cs` (new)
- `SABZ.Application/Services/CropSuitability/CropSuitabilityService.cs` (new)
- `SABZ.Infrastructure/Repositories/CropSuitabilityDataRepository.cs` (new)
- `SABZ.Infrastructure/Persistence/SeedData.cs` (requirements + regional rules re-enabled)
- `SABZ.Infrastructure/DependencyInjection.cs` (registrations)
- `SABZ.API/Controllers/CropSuitabilityController.cs` (new)
- `SABZ.API/appsettings.json` (+CropSuitability section)
- Migration `AddCropSuitabilityFoundation`

## 13. Testing

`test-crop-suitability.ps1` (idempotent, reuses fixtures): 25 checks covering
Rabi/Kharif evaluation, season auto-detection, no-GPS partial evaluation,
ownership 403, unknown farm 404, invalid season 400, unauthenticated 401,
descending ranking + threshold consistency, regressions (auth, locations,
farms, weather), and Ahmed Farm integrity before/after. **Result: 25/25 PASS.**

Example (Rawalpindi test farm, Rabi): Gram 85, Lentil 81, Wheat 76 —
sorted descending, levels consistent with thresholds.

## 14. Known limitations (by design, foundation scope)

- No agro-ecological zone (AEZ) data; geography is represented by
  administrative rules only.
- Soil compatibility uses a simple CSV list matched against free-text farm
  soil values; a normalized soil taxonomy join table is a future extension.
- No tehsil-level rules are seeded yet (the schema supports them).
- Water evaluation is coarse (irrigation present/absent vs Low/Medium/High).
- Climate uses the 7-day forecast envelope, not historical climate normals.
- Only 9 crops have requirement data; other catalog crops are excluded from
  results until their data is added.
- The score is an evaluation, not a yield prediction or a planting guarantee.

## 15. Extension points

- Add requirement rows for more crops/seasons — no code changes needed.
- Add district/tehsil rules to refine the location factor.
- Rotation-aware recommendations can later consume existing farm crop history
  (`ICropRepository`) — intentionally not part of this foundation.
- CSV soils can be replaced by a join table without touching the scoring flow.

## 16. Disclaimer

All suitability data in this foundation represents general agronomic knowledge
(PARC/FAO crop-zone publications and the initial SABZ dataset). It is
informational, not prescriptive advice; local conditions vary and expert
review is recommended.
