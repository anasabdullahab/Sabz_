# Prompt 6 — AI Crop Disease Identification & Agricultural Advice Foundation

## Purpose

Prompt 6 adds the first AI capability to SABZ: a farmer uploads a photograph
of a crop leaf/plant and receives a **cautious** disease assessment plus
agricultural guidance. The feature is built as a safe, data-driven,
provider-abstracted pipeline:

```
IMAGE → IMAGE VALIDATION → PLANT/LEAF RELEVANCE CHECK → DISEASE
        IDENTIFICATION → CONFIDENCE CHECK → AGRICULTURAL ADVICE
```

Hard rules enforced by design:

- SABZ **never invents a disease**. Every AI failure mode returns an honest
  error or an "uncertain" assessment.
- The disease model is **never called** for files that fail local validation
  or the plant-relevance gate (also a cost/quota protection).
- All AI output is labelled advisory ("Possible", "Likely", "AI assessment") —
  never a laboratory diagnosis.
- No chemical/pesticide dosages are ever produced; farmers are directed to
  approved product labels and local experts.

Prompt 4 (`GET /api/farms/{farmId}/crop-suitability`) and Prompt 5
(`GET /api/farms/{farmId}/crop-recommendations`) are **unchanged**.

## Provider selected & current configuration status

- **Provider**: Alibaba Cloud Model Studio (DashScope), OpenAI-compatible
  endpoint `https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions`.
- **Model**: `qwen-vl-max` (Qwen-VL multimodal vision-language model).
- **Role in pipeline**: one call performs BOTH the plant/leaf relevance check
  and the disease assessment (one image = one AI request).
- **Implementation status in this environment**: **NOT CONFIGURED**.
  No API key exists on this machine, so the endpoint returns a graceful
  `502` ("provider not configured") after all local validation. The full
  provider implementation is complete and activates by setting
  `DiseaseDetection:ApiKey` in the local (gitignored) `appsettings.json`
  or via `DiseaseDetection__ApiKey` environment variable.

### Free-tier facts (verified against Alibaba Cloud docs, Aug 2026)

| Aspect | Value |
| --- | --- |
| Free quota | 1,000,000 tokens per model (qwen-vl-max and qwen-vl-plus each) |
| Region | Singapore region only (International deployment scope) |
| Validity | 90 days from service activation — countdown is NOT paused by inactivity |
| After quota/expiry | Pay-as-you-go, unless the "Free Quota Only" switch is enabled (then calls fail with `AllocationQuota.FreeTierOnly` instead of charging) |
| Auth | Bearer API key (general-purpose pay-as-you-go key consumes free quota; Token-Plan keys do not) |

> **This is a time- and quota-limited free tier, not permanently free.**
> Enable "Free Quota Only" in the Model Studio console for the demo to avoid
> accidental charges. Provider pricing/availability was verified by reading
> official documentation only; live quota consumption could not be verified
> here because no key is available (**UNVERIFIED at runtime**).

### Provider abstraction (swappable)

SABZ depends only on `IPlantDiseaseDetectionProvider` (Application layer).
`QwenVisionDiseaseDetectionProvider` (Infrastructure) is one implementation;
replacing it (Hugging Face, self-hosted model, another vendor) requires only
a new Infrastructure class and a DI registration change — no Application/API
changes. The Application layer contains no HTTP SDK usage, provider classes,
API keys or provider URLs.

## Endpoint

```
POST /api/farms/{farmId}/disease-detection
Authorization: Bearer <JWT>
Content-Type: multipart/form-data
```

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `image` | file | yes | JPEG/PNG/WebP, ≤ 10 MB, ≥ 128×128 px |
| `cropId` | guid | no | Must belong to the same farm (403 otherwise) |
| `notes` | string | no | Free-text farmer observations passed to the model |

`userId`/`ownerId` are never accepted; identity comes from the JWT only.
Farm ownership follows the existing pattern: 401 unauthenticated,
404 farm not found, 403 foreign farm.

### Response (200)

```json
{
  "farmId": "...",
  "cropId": "...",
  "cropContext": { "cropName": "...", "season": "...", "growthStage": "...",
                   "plantingDate": "...", "catalogName": "...", "catalogCategory": "..." },
  "imageAssessment": { "imageAccepted": true, "isPlantImage": true,
                       "plantConfidence": 0.93, "message": "...",
                       "width": 1200, "height": 900, "format": "Jpeg",
                       "possiblyBlurry": false },
  "diseaseAssessment": { "detected": true, "assessmentLevel": "Likely",
                         "crop": "Wheat", "disease": "Leaf Rust",
                         "confidence": 0.81, "severity": "mild",
                         "explanation": "...", "assessmentSource": "AI model" },
  "advice": { "summary": "...", "recommendedActions": ["..."], "prevention": ["..."],
              "monitoring": ["..."],
              "adviceSources": ["AI model", "SABZ agricultural knowledge/reference data"] },
  "missingData": ["..."],
  "provider": { "name": "Alibaba Cloud Model Studio (DashScope)",
                "model": "qwen-vl-max", "version": null, "configured": true },
  "evaluatedAt": "...",
  "disclaimer": "SABZ AI assessments are advisory only..."
}
```

Non-plant image: `200` with `imageAssessment.isPlantImage = false` and the
message "Please upload a clear photograph of a crop leaf or plant." — the
disease model is never called and no disease is reported.

Low confidence: `diseaseAssessment.detected = false`,
`assessmentLevel = "Uncertain"`, advice asks for a clearer photograph and
gives monitoring guidance only.

## Image validation (local, before any AI call)

Implemented in `SharpImageValidator` (SixLabors.ImageSharp):

1. Non-empty file; size ≤ `MaxImageSizeMb` (10 MB; Kestrel/`FormOptions`
   limits enforce ~11 MB multipart bodies with 413 before upload completes).
2. Magic-byte sniffing (JPEG/PNG/WebP) — extension and Content-Type header
   are never trusted alone; a mismatched extension is rejected.
3. Full decode — corrupt/truncated files are rejected gracefully, never crash.
4. Dimension bounds (min 128×128, max 6000×6000, configurable).
5. Blur heuristic (Laplacian variance on a 256px grayscale copy) — reported
   as `possiblyBlurry` + `missingData` note, never a hard rejection.

## Confidence handling (configurable, centralized)

| Setting | Default | Effect |
| --- | --- | --- |
| `PlantConfidenceThreshold` | 0.6 | Below → treated as non-plant image (safe message, no disease call outcome used) |
| `MinimumDiseaseConfidence` | 0.4 | Below → `detected=false`, "Uncertain"; no disease name claimed |
| `HighConfidenceThreshold` | 0.7 | ≥ → "Likely", otherwise "Possible" |

Thresholds live in `DiseaseDetectionSettings` (section `DiseaseDetection`);
no magic numbers in code.

## Agricultural advice (data-driven)

Advice comes from two clearly labelled sources:

1. **AI model** — the provider's assessment/explanation.
2. **SABZ agricultural knowledge/reference data** — the `DiseaseInformations`
   table, matched by crop + disease name (exact, then bidirectional
   containment). No `switch` statement on disease names exists.

Seeded entries (6, labelled "Initial SABZ disease reference dataset (general
plant-health knowledge, expert review recommended)"): Wheat Leaf Rust, Rice
Blast, Tomato Early Blight, Tomato Leaf Curl Virus, Potato Late Blight,
Cotton Leaf Curl Virus. List fields (actions/prevention/monitoring) are
semicolon-separated and extensible without code changes.

No curated match → generic cautious guidance + `missingData` note. Chemical
dosages are never generated; every detected-disease response ends with
"follow only an approved product label or a local agricultural expert".

## Security

- `[Authorize]` + JWT-only user identity (same pattern as Prompts 4/5).
- Farm and crop ownership validation (404/403), crop must belong to the farm.
- Upload limits (size, type, dimensions), malformed-image handling.
- API key only via local config/environment; template carries a placeholder;
  nothing sensitive is committed. Keys and image content are never logged.
- Uploaded images are processed in memory only — nothing is persisted.

## Image storage decision

No images are stored. Prompt 6 has no existing storage architecture, and
persisting farmer photographs without a clear need would be a privacy risk.
Detection results are also **not persisted** in this phase — no
`DiseaseDetectionRecord` table was created. The only new table is
`DiseaseInformations` (curated advice), which is genuinely required for
data-driven guidance. A future phase may add detection history alongside
the satellite alert workflow.

## Configuration (`DiseaseDetection` section)

`Provider`, `Model`, `ApiBaseUrl`, `ApiKey` (local only), `TimeoutSeconds`,
`PlantConfidenceThreshold`, `HighConfidenceThreshold`,
`MinimumDiseaseConfidence`, `MaxImageSizeMb`, `AllowedImageTypes`,
`MinImageWidth/Height`, `MaxImageWidth/Height`, `BlurVarianceThreshold`.

## Error handling

All provider failures map to `DiseaseProviderException` → **502**:
not configured, unreachable, timeout, rate limit (429 upstream),
rejected credentials, invalid/empty provider response. Invalid uploads →
400 via `ValidationException` + the global exception middleware. No fake
results are ever produced.

## Performance

Exactly one AI call per request (relevance + disease in one vision call).
Local validation runs first so invalid/unrelated files cost zero AI quota.
Weather context reuses `IWeatherService` (cached) and is fetched at most
once; absence never blocks detection.

## Testing

`test-disease-detection.ps1` (local-only, idempotent, 39 checks) covers:
authenticated/unauthenticated/unknown-farm/foreign-farm, missing image,
unsupported file, oversized image, corrupted image, unrelated image,
valid plant image, crop context + crop ownership, no-GPS farm, provider
failure mode, Prompt 4 + Prompt 5 regression, Crop CRUD regression, and an
Ahmed Farm integrity guard. Result: **39/39 passed**.

Real-image note: the suite generates real decodable photographs locally
(System.Drawing). Checks tagged `[LIVE-PROVIDER]` in the script document
what must additionally be verified once a real API key is configured
(200 responses, `isPlantImage` behaviour, confidence tiers). To run live
tests, put a real diseased-leaf photo and an unrelated photo in the script's
temp paths or call Swagger with any real plant photograph.

## Known limitations

- **Provider not configured in this environment** — live AI behaviour is
  UNVERIFIED at runtime; all local pipeline behaviour is verified.
- Free quota is 90-day/1M-token limited (see table above).
- Blur detection is a heuristic (report-only).
- The relevance gate relies on the vision model's own assessment of
  "is this a plant?" (single-call design); a dedicated plant classifier can
  be added behind a second abstraction later.
- Curated advice dataset is intentionally small and needs expert review.

## Future satellite integration

The pipeline is deliberately service-based (`IDiseaseDetectionService.DetectAsync`)
so a future `SatelliteAlert → FarmerNotification → ImageRequest` flow can
invoke the identical disease-detection workflow without API changes. No
satellite code, tables or providers were implemented in Prompt 6.

## Components

| Layer | Component |
| --- | --- |
| Domain | `DiseaseInformation` entity, `DiseaseProviderException` |
| Application | `IPlantDiseaseDetectionProvider`, `PlantDiseaseDetectionRequest/Result`, `IImageValidator`, `IDiseaseDetectionService`, `IDiseaseInformationRepository`, `DiseaseDetectionSettings`, response DTOs, `DiseaseDetectionService` |
| Infrastructure | `QwenVisionDiseaseDetectionProvider`, `SharpImageValidator`, `DiseaseInformationRepository`, DI + named HttpClient, seed data |
| API | `DiseaseDetectionController`, middleware 502 mapping, multipart size limits |

Migration: `AddDiseaseDetectionFoundation` (additive: `DiseaseInformations`
table + 6 seed rows + index; zero changes to existing tables/data).
