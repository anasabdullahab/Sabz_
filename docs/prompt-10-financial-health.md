# Prompt 10: Farm Financial Health & Readiness Intelligence

## Purpose

Read-only financial intelligence computed **dynamically** from the Prompt 9
farmer-entered ledger: a farm/crop health summary, a category breakdown,
monthly activity, and a recorded-data completeness indicator. Everything is
aggregated in SQL at request time; **nothing derived is ever persisted**.

> **Financial Health & Readiness is calculated only from financial records
> entered into SABZ. It is not a loan, credit, banking, investment, or
> financing decision.**

**The system never invents financial data.** There is no estimation, no
auto-generation, no external sync, and **no AI** — every input row was typed
by the farmer through the Prompt 9 API, and every Prompt 10 number is a pure
aggregate of those rows.

## Hard rules honoured

- **No new tables, no migration, no schema change.** Prompt 10 is a pure read
  layer over `FinancialTransactions`; `TableCount` stays 15 before and after.
- **No persisted derived values.** No totals, scores, indicators, or
  completeness states are stored — each is recomputed per request.
- **No AI, no notifications, no background jobs, no schedulers.**
- **No loan/credit/banking/insurance/investment language or logic.** Wording
  is strictly factual and describes only recorded data.
- Ownership is derived `JWT user -> Farm (-> Crop) -> FinancialTransaction`;
  the client never supplies a `UserId`.
- Money stays `decimal` end-to-end.
- Aggregation is SQL-side (`SUM`/`COUNT`/`GROUP BY`/`MIN`/`MAX`,
  `AsNoTracking`) — no row-by-row loops, no N+1.
- The existing `IFinancialTransactionRepository` is **extended** (no
  duplicate repository); the same exception pipeline and `ISystemClock`-free
  read semantics are reused (reads need no clock).

## Health indicator (deterministic)

`FinancialHealthService.DetermineIndicator`, evaluated in order:

| Condition | Indicator |
| --- | --- |
| 0 transactions | `NoData` |
| < 5 transactions **or** no income **or** no expenses | `LimitedData` |
| net < 0 | `LossRecorded` |
| net == 0 | `BreakEven` |
| net > 0 | `PositiveNetResult` |

`netResult = totalIncome - totalExpenses`. Thresholds are named constants
(`LimitedDataTransactionThreshold = 5`). Explanations are factual statements
about recorded data only.

## Recorded-data completeness (NOT a credit score)

`GetCompletenessAsync` reads the farm's **full history** (no date-range
parameters by design) and scores five deterministic checks worth
**20 points each** (0–100):

| Check | Passes when |
| --- | --- |
| `TransactionsExist` | >= 1 transaction |
| `MinimumTransactionCount` | >= 10 transactions |
| `BothTypesRepresented` | income > 0 and expenses > 0 |
| `HistorySpan` | first->last date spans >= 30 days |
| `ActiveDays` | >= 3 distinct transaction dates |

Statuses: `NoData` (0 transactions), `Complete` (100), else `Partial`.
Every response carries the disclaimer
`"Based only on transactions entered into SABZ."` and three fixed limitation
lines stating it is not a credit/loan/insurance approval and that missing
records do not mean the farm has no income or expenses.

Constants: `CompletenessMinimumTransactions = 10`,
`CompletenessMinimumHistoryDays = 30`, `CompletenessMinimumActiveDays = 3`,
`CompletenessPointsPerCheck = 20`.

## Endpoints (all `[Authorize]`, user-scoped GETs)

| Route | Response |
| --- | --- |
| `GET /api/farms/{farmId}/financial-health?fromDate=&toDate=` | `FinancialHealthSummaryDto` — totals, counts, date bounds, active days, crop/farm split, indicator |
| `GET /api/farms/{farmId}/financial-health/categories?fromDate=&toDate=` | `CategoryBreakdownDto` — expense & income categories with dynamic percentages |
| `GET /api/farms/{farmId}/financial-health/activity?fromDate=&toDate=` | `FinancialActivityDto` — monthly `yyyy-MM` buckets + totals |
| `GET /api/farms/{farmId}/financial-health/completeness` | `FinancialCompletenessDto` — five checks, 0-100 score, disclaimer (no date params) |
| `GET /api/farms/{farmId}/crops/{cropId}/financial-health?fromDate=&toDate=` | `FinancialHealthSummaryDto` scoped to one crop |

Security semantics mirror Prompt 9: `401` without a token, `404` unknown
farm/crop, `403` another user's farm, `400` crop-of-another-farm and
`fromDate` after `toDate`. Responses expose `farmId`/`cropId` — never a user
id. Date params are normalised to UTC midnight; `fromDate`/`toDate` are
optional and independent.

## Repository extensions

Added to the existing `IFinancialTransactionRepository`
(records declared in the interface file):

- `FinancialHealthStats` + `GetHealthStatsAsync(...)` — totals/counts per type
  plus min/max date, distinct-date count, crop-related vs farm-level split
  (two SQL round-trips, both `GroupBy`).
- `CategoryTotalRow` + `GetCategoryTotalsAsync(...)` — `GroupBy` type+category.
- `MonthlyTotalRow` + `GetMonthlyTotalsAsync(...)` — `GroupBy`
  `TransactionDate.Year/.Month` + type (`yyyy-MM` formatted in the service).

All filters (farm, optional crop, optional date range) share one
`FilterForHealth` `AsNoTracking` query.

## Validation summary

| Rule | Result |
| --- | --- |
| Missing/invalid token | 401 |
| Unknown farm or crop | 404 |
| Another user's farm | 403 |
| Crop belongs to a different farm | 400 ("Selected crop does not belong to the selected farm.") |
| `fromDate` after `toDate` | 400 ("fromDate must be on or before toDate.") |

## Testing

`test-financial-health.ps1` (untracked, idempotent, self-cleaning; 95 checks,
green twice in a row): auth 401 ×6, ownership 404/403, crop-of-other-farm
400, NoData/LimitedData states, positive/loss/break-even indicators, date
filtering, category percentages & DB cross-check, monthly buckets, crop-scoped
health, completeness scores 0/20/40/60/80/100 across dedicated farms,
mandatory disclaimer & no-loan-wording checks, no `userId` leakage, Prompt 9
CRUD + Prompts 4-8 regressions, and cleanup/integrity (no leftover `FH`
farms, no orphans, no invented transactions).

## Components

| Component | Location |
| --- | --- |
| `IFinancialHealthService` / extended `IFinancialTransactionRepository` | `src/SABZ.Application/Interfaces` |
| `FinancialHealthSummaryDto`, `HealthCategoryDto`, `CategoryBreakdownDto`, `FinancialActivityPeriodDto`, `FinancialActivityDto`, `FinancialCompletenessCheckDto`, `FinancialCompletenessDto` | `src/SABZ.Application/DTOs/Financial` |
| `FinancialHealthService` | `src/SABZ.Application/Services/Financial` |
| `FinancialTransactionRepository` (health aggregates) | `src/SABZ.Infrastructure/Repositories` |
| `FinancialHealthController` | `src/SABZ.API/Controllers` |
| DI registration | `src/SABZ.Infrastructure/DependencyInjection.cs` |

## Deliberately NOT built

- Loans, credit, banking, insurance, investment, or financing features —
  this intelligence is expressly **not** any of those.
- AI/ML scoring, predictions, forecasting, or advice.
- Notifications, background jobs, schedulers, or external data providers.
- Any new table, migration, or persisted derived value.
- Any transaction not explicitly entered by the farmer.
