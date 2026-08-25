# Prompt 11: Farm Performance Dashboard & Decision Intelligence

## Purpose

Read-only performance intelligence computed **dynamically** from data that
already exists in SABZ: crop records, the Prompt 9 financial ledger and
Prompt 7 monitoring checks. It helps a farmer understand the recorded
performance of a farm: crop counts, the recorded financial position, which
crops have recorded financial activity, the best/weakest **recorded** net
results, and honest limitations of the recorded data.

> **Farm Performance is calculated only from data recorded in SABZ. It does
> not measure the farmer's real-world performance, farming skill, future
> outcomes, creditworthiness, or financial eligibility.**

**The system never invents data.** There is no estimation, no forecasting, no
external sync and **no AI** — every input row was created by the farmer
through existing SABZ endpoints, and every Prompt 11 value is a pure
aggregate of those rows. Nothing derived is ever persisted.

## Hard rules honoured

- **No new tables, no migration, no schema change.** Prompt 11 is a pure read
  layer over `Crops`, `FinancialTransactions` and `CropMonitoringChecks`;
  `TableCount` stays 15 before and after.
- **No persisted derived values.** No totals, rankings, statuses, activity
  scores or insights are stored — each is recomputed per request.
- **No AI, no notifications, no background jobs, no schedulers, no external
  providers.**
- **No loan/credit/banking/insurance/investment/budget/forecasting logic.**
  Wording is strictly factual ("best recorded net result", never "most
  profitable crop").
- Ownership is derived `JWT user -> Farm -> (Crops, FinancialTransactions,
  CropMonitoringChecks)`; the client never supplies a `UserId`/`OwnerId`.
- Money stays `decimal` end-to-end.
- Aggregation is SQL-side (`SUM`/`COUNT`/`GROUP BY`/`MIN`/`MAX`,
  `AsNoTracking`) — no full-history loads, no N+1.
- Existing repositories are **extended** (no duplicate repositories); Prompt
  9/10 calculations are reused (`GetHealthStatsAsync`), never duplicated.

## Endpoints (all `[Authorize]`, user-scoped GETs)

| Route | Response |
| --- | --- |
| `GET /api/farms/{farmId}/performance?fromDate=&toDate=` | `FarmPerformanceSummaryDto` — crop counts, recorded totals, best/weakest recorded crop, overall status, structured limitations |
| `GET /api/farms/{farmId}/performance/crops?fromDate=&toDate=` | `List<CropPerformanceDto>` — per-crop totals, net result, `FinancialDataStatus` |
| `GET /api/farms/{farmId}/performance/activity` | `FarmActivitySummaryDto` — recorded activity in SABZ over the full history |

### Date filtering semantics

`fromDate`/`toDate` are optional, independent, UTC date-only values with the
Prompt 9/10 validation (`fromDate` must be on or before `toDate`, otherwise
400). **The range filters only financial ledger rows** (`TransactionDate`);
crop records and monitoring checks are never range-filtered. The activity
endpoint has no date parameters by design (full recorded history, mirroring
the Prompt 10 completeness endpoint).

## Overall performance status (deterministic, NOT a score)

Evaluated in order in `FarmPerformanceService.DetermineOverallStatus`:

| Condition | Status |
| --- | --- |
| 0 financial transactions **and** 0 completed/skipped monitoring checks | `NoRecordedData` |
| >= 5 transactions **and** income > 0 **and** expenses > 0 | `RecordedActivityAvailable` |
| everything in between | `LimitedRecordedData` |

`SufficientTransactionThreshold = 5` (same threshold as the Prompt 10
LimitedData rule). The status describes **recorded data sufficiency only** —
never farming skill, creditworthiness, risk or future success. Every response
carries a factual `statusExplanation`.

## Crop financial data statuses (deterministic)

Per crop, from recorded ledger sides within the effective range:

| Income rows | Expense rows | `FinancialDataStatus` |
| --- | --- | --- |
| 0 | 0 | `NoFinancialData` |
| 0 | > 0 | `ExpensesOnly` |
| > 0 | 0 | `IncomeOnly` |
| > 0 | > 0 | `RecordedIncomeAndExpenses` |

The missing side is never invented.

## Ranking behavior

- A crop is **qualifying** only if it has >= 1 recorded transaction linked
  via `FinancialTransaction.CropId` within the effective range; crops without
  records are never ranked and `bestRecordedCrop`/`weakestRecordedCrop` stay
  `null` when no crop qualifies (with a `NoRankedCrops` limitation).
- `NetResult = TotalIncome - TotalExpense` (decimal).
- **Best**: highest net result; **weakest**: lowest net result. With a single
  qualifying crop both point to the same crop — this is factual.
- **Deterministic tie-break**: net result, then crop name (ordinal ascending),
  then crop id. Identical nets always resolve to the same crop on every call.
- Farm-level transactions (`CropId = null`) count toward farm totals but are
  excluded from the per-crop ranking and reported as an
  `UnattributedTransactions` limitation.
- Wording is always "best/weakest **recorded** net result" — recorded rows
  only, never objective real-world profitability.

## Structured limitations

`limitations` is a structured collection of `{ code, message }` pairs; codes
are stable and factual:

| Code | Emitted when |
| --- | --- |
| `NoFinancialTransactions` | 0 transactions in the effective range |
| `CropsWithoutFinancialRecords` | at least one crop has no recorded transactions |
| `ExpensesOnlyCrops` | crops with recorded expenses but no income (names listed) |
| `IncomeOnlyCrops` | crops with recorded income but no expenses (names listed) |
| `UnattributedTransactions` | transactions not linked to a crop exist |
| `NoRankedCrops` | no crop qualifies for the ranking |

Every response also carries the mandatory `disclaimer`: *"Based only on data
recorded in SABZ. This does not measure real-world farm performance, farming
skill, future outcomes, creditworthiness, or financial eligibility."*

> "Farm Performance is calculated only from data recorded in SABZ. It does not
> measure the farmer's real-world performance, farming skill, future outcomes,
> creditworthiness, or financial eligibility."

## Recorded activity definition

`GET .../performance/activity` summarizes **recorded activity in SABZ**,
never physical farm activity. Recorded activity events are:

- financial transactions (by `TransactionDate`)
- completed monitoring checks (by `CompletedAt`)
- skipped monitoring checks (by `SkippedAt`)

Scheduled checks are plans, not events: they are counted separately
(`scheduledMonitoringChecks`) and never contribute to activity dates.
`firstRecordedActivity`/`latestRecordedActivity` are the min/max event date;
`recordedActivityDays` counts distinct calendar days with at least one event
(transaction days aggregated in SQL via `DISTINCT`, check event days merged
in memory). The `explanation` states plainly that this is recorded activity
in SABZ only.

## Security

Mirrors Prompts 9/10: `401` without/with an invalid token, `404` unknown
farm, `403` another user's farm. Responses expose `farmId`/`cropId` — never a
user id. `UserId`/`OwnerId` are never accepted from the client.

## Performance approach

- Overview: 4 queries (crops, ledger health stats, per-crop `GROUP BY`
  totals, monitoring lifecycle projection) — no N+1.
- Crop breakdown: 2 queries (crops, per-crop totals).
- Activity: 3 queries (distinct transaction dates, ledger stats, monitoring
  lifecycle projection).
- New repository methods extend existing repositories:
  `IFinancialTransactionRepository.GetCropTotalsAsync` /
  `GetDistinctTransactionDatesAsync` and
  `ICropMonitoringCheckRepository.GetFarmCheckEventsAsync` (SQL projection of
  status + lifecycle timestamps only — full check entities are never loaded).

## Data sources

| Source | Used for |
| --- | --- |
| `Crops` | crop counts, statuses, names, ranking join |
| `FinancialTransactions` (Prompt 9) | totals, counts, per-crop results, activity events |
| `CropMonitoringChecks` (Prompt 7) | check counts by status, activity events |

## Components

| Component | Location |
| --- | --- |
| `IFarmPerformanceService` + extended repository interfaces | `src/SABZ.Application/Interfaces` |
| `FarmPerformanceSummaryDto`, `RecordedCropPerformanceDto`, `CropPerformanceDto`, `FarmActivitySummaryDto`, `PerformanceLimitationDto` | `src/SABZ.Application/DTOs/Performance` |
| `FarmPerformanceService` | `src/SABZ.Application/Services/Performance` |
| `FinancialTransactionRepository` / `CropMonitoringCheckRepository` (Prompt 11 aggregates) | `src/SABZ.Infrastructure/Repositories` |
| `FarmPerformanceController` | `src/SABZ.API/Controllers` |
| DI registration | `src/SABZ.Infrastructure/DependencyInjection.cs` |

## Testing

`test-farm-performance.ps1` (untracked, idempotent, self-cleaning): auth 401
checks, ownership 404/403, all three overall statuses, correct totals and net
results, all four crop financial data statuses, best/weakest ranking with
crops-without-records excluded and deterministic tie handling, date-range
filtering and validation, activity semantics (completed/skipped/scheduled,
first/latest/days), structured limitations, no `userId` leakage, and
cleanup/integrity checks.

## Deliberately NOT built

- Loans, credit scoring, loan approvals, banking, insurance decisions,
  investment advice, payment processing, or budgets.
- Forecasting, predictions, or fake/inferred profitability.
- AI-generated financial advice or any external provider.
- Background jobs, schedulers (Hangfire/Quartz), or notifications.
- Satellite monitoring or weather automation.
- Any new table, migration, or persisted derived value.
- Any transaction, crop activity, or monitoring event that was not created
  by the farmer through existing SABZ endpoints.
