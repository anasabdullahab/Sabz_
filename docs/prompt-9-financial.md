# Prompt 9: Farm Profit & Loss (P&L) Financial Ledger

## Purpose

A farmer-owned financial ledger: income and expense records entered
**exclusively by the farmer**, plus dynamically computed profit & loss
summaries per farm (optionally per crop and/or date range). This is the
foundation for later financial intelligence (Prompt 10 compatibility is
preserved without building any of it).

**The system never invents financial data.** There is no auto-generation,
no estimation, no sync from any external source — every row was typed by
the farmer through the API.

## Hard rules honoured

- Ownership is derived `JWT user -> Farm -> FinancialTransaction`. There is
  **no `UserId`/`OwnerId` column** on the transaction and no client field for
  it; a transaction belongs to a farm, and the farm belongs to the user.
- An optional `CropId` must belong to the same farm (400 otherwise).
- Money is `decimal` end-to-end (never `float`/`double`), stored as
  `decimal(18,2)`, strictly positive, capped at **PKR 1,000,000,000**.
- P&L is **computed dynamically** from raw transactions on every request:
  `netProfitLoss = totalIncome - totalExpenses`. No totals, balances, or
  derived financial state are ever persisted.
- `TransactionDate` (when the financial event happened, farmer-supplied,
  future dates **rejected**) is kept distinct from `CreatedAt` (when SABZ
  stored the row) and `UpdatedAt`.
- Categories are C# string constants validated via `HashSet` membership —
  data-driven, no switch statements, no crop names in financial logic.
- Time handling reuses the existing `ISystemClock` singleton.
- Validation errors surface through the existing SABZ exception pipeline
  (`ValidationException` -> 400, `NotFoundException` -> 404,
  `ForbiddenException` -> 403).

## Data model

`FinancialTransaction` (`src/SABZ.Domain/Entities/FinancialTransaction.cs`):

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | Primary key |
| `FarmId` | `Guid` | FK -> `Farms`, **cascade delete** (farm removal removes its ledger) |
| `CropId` | `Guid?` | FK -> `Crops`, optional; must belong to the same farm |
| `TransactionType` | enum-as-string | `Income` or `Expense` (existing SABZ enum convention) |
| `Category` | `string(100)` | One of `TransactionCategories` matching the type |
| `Amount` | `decimal(18,2)` | Positive, max 1,000,000,000 |
| `TransactionDate` | `DateTime` UTC midnight | Farmer date; future dates rejected |
| `Notes` | `string(1000)?` | Optional free text |
| `CreatedAt` / `UpdatedAt` | `DateTime` UTC | `CreatedAt` defaults to `GETUTCDATE()` |

Indexes: `(FarmId, TransactionDate)`, `(FarmId, TransactionType)`, `(CropId)`
— the P&L read paths.

### Crop-delete semantics (documented deviation)

The approved design is *farm delete cascades the ledger; crop delete keeps
history with the crop link removed (SetNull)*. SQL Server rejects a
database-level `SET NULL` here (error 1785: the second cascade path
`Farms -> Crops -> FinancialTransactions` is illegal), so:

- `FarmId` FK is `ON DELETE CASCADE` in the database.
- `CropId` FK is `RESTRICT` in the database, and the SetNull semantics are
  enforced **in the application layer**: `CropService.DeleteCropAsync` stages
  `CropId = null` on the crop's transactions
  (`IFinancialTransactionRepository.NullifyCropReferencesAsync`) and saves
  atomically with the crop removal. Financial history always survives crop
  deletion; it is only ever deleted with its farm.

### Categories (`TransactionCategories`)

Expenses: `Seeds`, `Fertilizer`, `Labour`, `Irrigation`, `Equipment`,
`Machinery`, `Fuel`, `Transport`, `PestDiseaseManagement`, `OtherExpense`.
Income: `CropSale`, `LivestockIncome`, `OtherIncome`.

Validation = set membership AND type match (an income category cannot be used
on an expense and vice versa). Extending = add constants; no logic changes.

## Endpoints (all JWT-only, user-scoped)

| Method & route | Response |
| --- | --- |
| `POST /api/farms/{farmId}/transactions` | Created transaction DTO |
| `GET /api/farms/{farmId}/transactions?type=&category=&cropId=&fromDate=&toDate=&take=` | Farm's transactions, newest `TransactionDate` first; `take` default 50, max 100 |
| `GET /api/transactions/{id}` | One transaction DTO (ownership verified) |
| `PUT /api/transactions/{id}` | Full replacement: every field re-validated, farm/crop ownership re-checked |
| `DELETE /api/transactions/{id}` | 204 |
| `GET /api/farms/{farmId}/financial-summary?cropId=&fromDate=&toDate=` | `{ farmId, cropId, fromDate, toDate, totalIncome, totalExpenses, netProfitLoss, transactionCount }` — computed, never stored |

Security semantics: `401` without a token on every endpoint, `404` for
unknown farm/transaction/crop ids, `403` for another user's farm or
transaction (explicit IDOR protection), `400` for validation failures.
Response DTOs expose `farmId`, `cropId`, `cropName` — never a user id.

## Validation summary

| Rule | Result |
| --- | --- |
| Amount <= 0 or > 1,000,000,000 | 400 |
| Future or missing `TransactionDate` | 400 |
| Unknown transaction type (only `Income`/`Expense`) | 400 |
| Unknown category, or category mismatched to type | 400 |
| Notes over 1000 chars | 400 |
| Crop not found | 404 |
| Crop belongs to a different farm | 400 ("Selected crop does not belong to the selected farm.") |
| `take` <= 0 | 400 |
| `fromDate` after `toDate` | 400 |

## Migration

`20260821174408_AddFinancialTransactions` — purely additive: creates
`FinancialTransactions` with the two FKs and three indexes above. `Down()`
removes only the Prompt 9 table. Row-count snapshots before/after confirm
Prompts 1–8 data unchanged.

## Testing

`test-financial.ps1` (untracked, idempotent, self-cleaning; 88 checks, green
twice in a row): create/echo of income & expense rows, no `userId` in JSON,
all validation rejections, list filters (`type`/`category`/`cropId`/date
range), `take` semantics, dynamic P&L summaries (whole farm / crop-scoped /
date-ranged), auth 401 ×6, IDOR 403/404, full PUT replacement with ownership
re-validation, crop-delete SetNull survival, farm-delete cascade, user/farm
isolation, Prompts 4–8 regressions (provinces, monitoring, notifications,
crop/farm endpoints, Ahmed Farm guard, no invented data), and DB integrity
(orphan checks, indexes, `decimal(18,2)` money type).

## Components

| Component | Location |
| --- | --- |
| `FinancialTransaction`, `FinancialTransactionType`, `TransactionCategories` | `src/SABZ.Domain/Entities` |
| `IFinancialTransactionRepository` / `IFinancialService` | `src/SABZ.Application/Interfaces` |
| `CreateFinancialTransactionDto`, `UpdateFinancialTransactionDto`, `FinancialTransactionResponseDto`, `FinancialSummaryResponseDto` | `src/SABZ.Application/DTOs/Financial` |
| `FinancialService` | `src/SABZ.Application/Services/Financial` |
| `FinancialTransactionRepository` | `src/SABZ.Infrastructure/Repositories` |
| `FinancialTransactionsController` | `src/SABZ.API/Controllers` |
| Crop-delete SetNull hook | `CropService.DeleteCropAsync` |

## Deliberately NOT built

- Credit scoring, insurance readiness, loan/banking/payment integration
  (Prompt 10 territory — the ledger is its raw-data foundation only).
- Recurring transactions, budgets, forecasting, AI financial advice.
- Background jobs, schedulers, or any external financial data provider.
- Persisted totals/balances of any kind.
- Any transaction not explicitly entered by the farmer.
