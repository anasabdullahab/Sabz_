# Prompt 13: Voice-First AI Agronomist Assistant

## Purpose

A secure, farm-aware AI assistant that answers agriculture questions from a
farmer's **text message or voice recording**, in English or Urdu. It combines
the farmer's question with a **focused, read-only snapshot** of their own farm
(profile, active crops, optional external weather, and - only for
disease-related questions - curated disease reference names) and returns a
structured, disclaimer-bearing answer.

> **The SABZ AI Agronomist provides informational assistance based on the
> farmer's question and available SABZ or external data. It does not physically
> inspect the farm, guarantee outcomes, automatically diagnose diseases, or
> perform actions on behalf of the farmer.**

**The assistant is strictly read-only.** It never creates, updates or deletes
farms, crops, transactions, monitoring checks, notifications or users. There is
no chat history table, no AI memory of previous conversations, and no
background job, queue, Hangfire or Quartz infrastructure.

## Hard rules honoured

- **No new tables, no migration, no schema change.** `TableCount` stays 16
  before and after and `dotnet ef migrations has-pending-model-changes`
  reports no changes. Questions, answers, transcriptions and audio are
  processed per request and never persisted.
- **Reuse the existing Prompt 6 AI infrastructure, never duplicate it.** The
  agronomist reuses the shared DashScope connection already configured under
  `DiseaseDetection` (`ApiBaseUrl`, `ApiKey`, `TimeoutSeconds`). No second API
  key, no duplicated HTTP-client/provider plumbing; the `Agronomist` config
  section only adds model names and input limits.
- **Provider HTTP logic lives in Infrastructure only.** The controller and the
  Application service never touch HTTP details; they depend on
  `IAgronomistAiProvider` and `ISpeechToTextProvider` abstractions.
- **Honest failure, never fake results.** With no API key configured, both
  endpoints return a controlled `502` "not configured" error - never a
  fabricated answer or a fabricated transcription.
- **Focused context, never a data dump.** Only the requested farm's profile,
  its bounded list of active crops, optional weather and (for disease-related
  questions only) a bounded list of curated disease names are supplied to the
  AI. Financial ledger, monitoring history and notifications are never included.
- **Data provenance is distinguished.** The system prompt separates
  SABZ-recorded facts, farmer-provided input, external weather (clearly marked
  as external) and general agronomic knowledge, and forbids inventing data.
- Ownership is derived `JWT user -> Farm`; the client never supplies a
  `UserId`/`OwnerId`, and no user id, JWT detail, API key or system prompt is
  ever exposed in responses or errors.

## Endpoints (`[Authorize]`, farm-scoped POST)

| Route | Body | Response |
| --- | --- | --- |
| `POST /api/farms/{farmId}/agronomist/chat` | JSON `{ "message": "..." }` | `AgronomistResponseDto` |
| `POST /api/farms/{farmId}/agronomist/voice` | multipart `audio` file | `VoiceAgronomistResponseDto` (adds `transcription`, `transcriptionProvider`) |

### Response shape (both flows)

| Field | Content |
| --- | --- |
| `question` | The question answered (text input, or the voice transcription) |
| `answer` | The AI-generated agronomy guidance (informational only) |
| `language` | Detected/responded language code (`"en"` or `"ur"`) |
| `farmContextUsed` | The focused context actually supplied: farm profile, bounded `activeCrops`, `weatherIncluded` + `weatherSummary` (external, when available) |
| `limitations` | Structured `{ code, message }` data-context facts (see below) |
| `disclaimer` | The mandatory advisory disclaimer quoted at the top of this document |
| `generatedAt` | UTC generation time |

## Voice flow

1. `audio` part validated: non-empty, ≤ `MaxAudioSizeMb` (10 MB), content type
   in `AllowedAudioTypes` (wav/mp3/m4a/flac/ogg variants) → otherwise `400`.
2. `ISpeechToTextProvider` transcribes the audio in memory (base64 multimodal
   payload through the same OpenAI-compatible DashScope endpoint used by the
   Prompt 6 vision provider, model `qwen2-audio-instruct`). Audio is never
   written to disk or stored.
3. The transcription becomes the question (truncated to the text limit if
   needed); an empty transcription is a controlled `400`, never a fake answer.
4. The identical farm-aware answer pipeline runs and the response additionally
   carries `transcription` + `transcriptionProvider`.

Text-to-speech output is intentionally out of scope; the required voice
feature is voice-in → transcription → structured answer.

## Context pipeline

`AgronomistAssistantService` (Application layer) orchestrates:

1. **Ownership** (`404` unknown farm, `403` another user's farm).
2. **Input validation** (`400` empty/whitespace question, question over
   `MaxQuestionLength` (1000), audio violations).
3. **Provider gate** — without a configured key: controlled `502`
   ("not configured"); no context is built, nothing is faked.
4. **Context build** — farm profile + active crops (`Status = "Active"`,
   bounded by `MaxActiveCropsInContext`); external weather via the existing
   `IWeatherService` in a guarded try/catch (failure adds a
   `WeatherUnavailable` limitation and never breaks the request); curated
   disease names via `IDiseaseInformationRepository` only when the question is
   disease-related, bounded by `MaxDiseaseReferencesInContext`.
5. **Language detection** — Arabic-script heuristic (`ur`) else `en`; the AI is
   instructed to answer in the farmer's language.
6. **AI completion** via `IAgronomistAiProvider` (chat model `qwen-plus`,
   temperature 0.3, system prompt carrying the read-only/provenance rules +
   user prompt).

## Structured limitations

`limitations` is a list of `{ code, message }` pairs; codes are stable:

| Code | Emitted when |
| --- | --- |
| `RecordedDataOnly` | always (first) — context reflects only SABZ-recorded + external weather data |
| `NoCrops` | the farm has no active crop records |
| `NoCoordinates` | the farm has no GPS coordinates (weather not attempted) |
| `WeatherUnavailable` | coordinates exist but weather retrieval failed |

Every response also carries the mandatory `disclaimer` quoted at the top of
this document.

## Configuration (`appsettings.json`)

The shared DashScope connection stays under `DiseaseDetection`
(`ApiBaseUrl`/`ApiKey`/`TimeoutSeconds`). The new section adds only:

```json
"Agronomist": {
  "ChatModel": "qwen-plus",
  "SpeechToTextModel": "qwen2-audio-instruct",
  "MaxQuestionLength": 1000,
  "MaxAudioSizeMb": 10,
  "AllowedAudioTypes": [ "audio/wav", "audio/x-wav", "audio/wave", "audio/vnd.wave",
                         "audio/mpeg", "audio/mp3", "audio/mp4", "audio/x-m4a",
                         "audio/flac", "audio/ogg" ],
  "MaxActiveCropsInContext": 10,
  "MaxDiseaseReferencesInContext": 6
}
```

Kestrel request limits were raised to 11,000,000 bytes
(`MultipartBodyLengthLimit` / `MaxRequestBodySize` in `Program.cs`) so the
10 MB audio limit is enforced by the service with a deterministic `400`.

## Security

Mirrors the existing farm endpoints: `401` without/with an invalid token,
`404` unknown farm, `403` another user's farm, `400` invalid input, `502`
provider failure (mapped by `GlobalExceptionMiddleware` from
`AgronomistProviderException`). Provider errors never leak keys, prompts or
user ids. `CancellationToken` flows through the controller to the providers.

## Testing

`test-agronomist.ps1` (idempotent, run twice consecutively) covers:
authentication (`401`), ownership (`404`/`403`), text validation, voice
validation (missing/empty/unsupported/oversized audio), the controlled `502`
not-configured gate for both flows (no fabricated answer/transcription, no
`userId`/`ownerId` leakage), the **read-only guarantee** (row counts for
crops/transactions/monitoring/notifications/farms unchanged after repeated
chat + voice calls, and no chat-history table exists), ownership isolation and
cleanup/integrity guards (16 tables, 9 migrations, Ahmed seed farm untouched).
Checks tagged `[LIVE-PROVIDER]` document what must be verified once a real
DashScope API key is supplied locally.
