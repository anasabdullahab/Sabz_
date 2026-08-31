# Prompt 17 — Crop Price Intelligence

> **Mandatory statement:** Crop prices shown by SABZ are informational market
> data. Prices may change and SABZ does not predict prices, guarantee future
> prices, or provide financial, investment, or trading advice.

## Purpose

Provide authenticated farmers with factual, read-only agricultural commodity
price information (crop, location, market, price, unit, price date, source).
The feature is **informational only**: it never predicts prices, recommends
buying or selling, guarantees profit, gives investment advice, or touches the
financial ledger, marketplace, notifications, or any database table.

## Endpoints

Both endpoints require a JWT bearer token (`[Authorize]`), accept **no user
id**, require **no farm**, and perform **no writes**.

| Method & path | Purpose |
| --- | --- |
| `GET /api/crop-prices` | Paginated, filterable price feed |
| `GET /api/crop-prices/{cropName}` | Latest price + dated history for one crop |

### `GET /api/crop-prices` query parameters

| Parameter | Behaviour |
| --- | --- |
| `crop` | Resolved against the existing CropCatalog (22 crops), case-insensitive with safe normalisation (`gram chickpea` → `Gram (Chickpea)`). Unknown crop → honest empty result, never fabricated prices. |
| `province`, `district` | Exact match, case-insensitive, trimmed. |
| `market` | Substring match, case-insensitive. |
| `fromDate`, `toDate` | `yyyy-MM-dd`, inclusive on both ends. `fromDate > toDate` → 400. |
| `page`, `pageSize` | Defaults `1` / `20`; `pageSize` max `50`. `page < 1`, `pageSize < 1`, `pageSize > 50` → 400. |

Response: `{ items[], page, pageSize, totalCount, totalPages, dataStatus, disclaimer }`.
Each item: `{ cropName, province, district, market, price, unit, priceDate, source, dataStatus, disclaimer }`.

### `GET /api/crop-prices/{cropName}`

Optional `fromDate`/`toDate` filters. Unknown crop → **404**. Recognised crop
with no data → honest `Unavailable` result with a message (never a placeholder).
Otherwise: `{ cropName, cropRecognized, latest, historicalRecords[], firstDate, latestDate, dataStatus, disclaimer }`.

A deliberately minimal API surface: no farm-specific price endpoint was built,
because the same information is available via filters and adding it would grow
the surface without new capability.

## Provider architecture

Prices come from an `ICropPriceProvider` abstraction so the source is
replaceable without touching the service, controller, or consumers:

```
CropPriceController  →  CropPriceService  →  ICropPriceProvider
                              │                     │
                              │              ReferenceCropPriceProvider (current)
                              └── ICropSuitabilityDataRepository.GetCatalogAsync()
                                  (crop-name resolution, no second catalog)
```

- `ICropPriceProvider` exposes `SourceName`, `IsLive`, and `GetRecordsAsync`.
- `CropPriceService` validates, filters deterministically, stamps the
  disclaimer on every record, and reports `dataStatus = Live` only when the
  provider's `IsLive` is genuinely true.
- Provider failures throw `CropPriceProviderException` (domain), mapped by
  `GlobalExceptionMiddleware` to **HTTP 502** with
  `{"message":"...","code":"CropPriceProviderUnavailable"}` — no stack traces,
  no connection strings, no internal details.

## AMIS decision (why there is no live source)

AMIS Punjab (www.amis.pk) was inspected before implementation. Findings:

- The site is an ASP.NET WebForms application (`ViewPrices.aspx`,
  `BrowsePrices.aspx`) driven by ViewState/postbacks and dropdown selections —
  there is **no stable machine-readable (JSON/REST) endpoint**.
- The only structured consumer channel is an Android app.
- AMIS publishes prices in the "Rs/100Kg" convention.

Scraping fragile WebForms HTML or inventing an endpoint was explicitly ruled
out by the requirements, so the provider abstraction ships with a clearly
labelled **reference provider** instead. Swapping in a real live provider
later requires only a new `ICropPriceProvider` implementation and one DI line.

## Current provider: SABZ Reference Dataset (non-live)

`ReferenceCropPriceProvider` — clearly labelled, deterministic, never
presented as live:

- `SourceName = "SABZ Reference Dataset"`, `IsLive = false`.
- 16 of the 22 catalog crops, 8 Punjab districts (Lahore, Faisalabad, Multan,
  Rawalpindi, Gujranwala, Bahawalpur, Sahiwal, Sargodha), 220 records.
- Fixed reference window **2026-08-20 … 2026-08-24** (not "today's" prices).
- Deterministic arithmetic variation: `price = base + day×25 + districtIndex×15`.
- Unit preserved as published by the source convention: **100Kg** (no blind
  unit conversion anywhere).
- Catalog crops without reference data (Lentil, Tobacco, Date Palm, Apple,
  Mung bean, Mash bean) honestly report `Unavailable` with a message.

## `dataStatus` meanings

| Value | Meaning |
| --- | --- |
| `Live` | Only when a provider genuinely returns current live data. Never used by the reference provider. |
| `Historical` | Dated past records from a real source. |
| `Reference` | Current state: clearly-labelled, non-live reference dataset. |
| `Unavailable` | No data for the requested crop/filter combination — honest emptiness, never fabricated prices. |

## Validation summary

- `fromDate > toDate` → 400; unparseable date strings → 400.
- `page < 1`, `pageSize < 1`, `pageSize > 50` → 400.
- Blank crop name on the detail endpoint → 400; unknown crop → 404.
- Provider failure → 502 with a safe structured body.

## Authentication & security

- All endpoints `[Authorize]`; missing/malformed token → 401.
- No endpoint accepts or exposes a user id, farm id, or any identity/secret
  material; responses contain only market data fields.
- Error responses are structured and leak no internals.

## Database impact

**Zero.** No new tables, no migrations, no seed changes: 21 tables and 11
migrations before and after, verified with
`dotnet ef migrations has-pending-model-changes` (clean). No CropPrices,
PriceHistory, MarketPrices, or cache tables — prices are never persisted,
including never persisting fabricated/derived values. No API key and no
persistent caching were introduced.

## Financial isolation

The feature is strictly read-only: it creates no `FinancialTransaction`,
never modifies the ledger, creates no marketplace listings/conversations, no
notifications, and schedules no background jobs. Verified by test (T10).

## Limitations

- Data is reference (non-live), clearly labelled as such on every record and
  every response envelope.
- Coverage: 16 crops, 8 Punjab districts, one 5-day window.
- No farm-specific pricing, no unit conversion, no history beyond the
  provider window, no alerting.

## What was deliberately NOT built

- No Prompt 18, no satellite/NDVI, no offline sync.
- No payments, orders, trading, buy/sell recommendations.
- No price prediction or AI forecasting.
- No notifications / SMS / WhatsApp / TTS for prices.
- No frontend, no scraping of AMIS HTML, no invented AMIS endpoint.

## Testing

`test-crop-prices.ps1` (idempotent, self-cleaning by construction — the
feature writes nothing): T1 auth, T2 basic endpoint, T3 crop filtering,
T4 location filtering, T5 date filtering, T6 pagination, T7 source
transparency, T8 provider failure behaviour, T9 security, T10 financial
isolation, T11 database integrity, T12 mandatory disclaimer — **48/48 passed,
run twice consecutively**. All 15 regression suites (Prompts 1–16) pass with
no baseline edits.
