# Prompt 16: Precision Crop Input & Dosage Calculator

## Purpose

A deterministic helper that answers one question for a farmer standing in
front of their field: *"I have this farm area and this dosage rate - how much
input do I need?"* The calculator multiplies the **authoritative recorded
farm area** by the **farmer-supplied dosage rate** and returns the required
quantity, converting the area between acres and hectares when necessary.

> **The SABZ input calculator performs arithmetic using the area and dosage
> rate supplied to it. It does not determine whether a dosage rate is
> appropriate for a particular crop, pest, disease, product, formulation,
> soil, weather condition, or local regulation. Farmers must follow the
> product label and applicable agricultural guidance.**

The feature is deliberately **pure arithmetic on demand**:

- **No AI** - no Qwen/DashScope/OpenAI call, no generated or prescribed
  dosage rates. The rate is always exactly the one the farmer typed.
- **No persistence** - nothing is stored; there is no calculation history,
  **no new table, no migration, no schema change** (21 tables / 11
  migrations before and after).
- **No side effects** - no notifications, no financial transactions, no
  marketplace/community/monitoring interaction, no background jobs.

## Endpoint

Exactly one endpoint, JWT-protected like the rest of SABZ:

| Route | Behaviour |
| --- | --- |
| `POST /api/farms/{farmId}/input-calculator` | Calculates `farm area × dosage rate` for one of the caller's own farms and returns the quantity plus a human-readable formula. Pure read path - writes nothing. |

### Request

```json
{
  "cropId": "optional guid",
  "inputName": "Urea",
  "category": "Fertilizer",
  "dosageRate": 2,
  "dosageUnit": "Kg",
  "dosageBasis": "PerAcre"
}
```

- The client **never supplies the farm area**: the authoritative area and
  unit are read from the `Farm` record (`FarmSize` / `FarmSizeUnit`).
- The client **never supplies a user id**: ownership comes from the JWT
  (`ClaimTypes.NameIdentifier`), consistent with every other SABZ feature.
- `cropId` is optional context only; when present it must exist and must
  belong to the same farm (foreign-farm crops → `400`, unknown crops → `404`).

Controlled values (case-insensitive input, canonical casing returned,
anything else → `400`) live in `InputCalculatorValues` (same convention as
`MarketplaceValues` / `TransactionCategories`):

| Field | Allowed values |
| --- | --- |
| `category` | Fertilizer, Pesticide, Herbicide, Fungicide, Insecticide, Other |
| `dosageUnit` | Kg, Liters, Grams, Milliliters |
| `dosageBasis` | PerAcre, PerHectare |
| farm area unit (stored) | Acres, Hectares (singular forms accepted) |

### Response

```json
{
  "farmId": "...", "cropId": null, "inputName": "Urea", "category": "Fertilizer",
  "farmArea": 5, "farmAreaUnit": "Acres",
  "calculationArea": 5, "calculationAreaUnit": "Acres",
  "dosageRate": 2, "dosageUnit": "Kg", "dosageBasis": "PerAcre",
  "requiredQuantity": 10, "requiredQuantityUnit": "Kg",
  "conversionApplied": false,
  "calculationFormula": "5 Acres × 2 Kg/acre = 10 Kg",
  "disclaimer": "The calculation only applies the supplied dosage rate ..."
}
```

Every response carries the disclaimer reminding the farmer that SABZ only
applied their numbers and that the product label governs.

## Calculation rules

- **Formula:** `requiredQuantity = calculationArea × dosageRate`, all
  `decimal` arithmetic end to end.
- **Area conversion** happens only when the farm's stored unit differs from
  the dosage basis, using fixed documented constants
  (`InputCalculatorService`):
  - `1 hectare = 2.47105 acres` (`HectareToAcreFactor`)
  - `1 acre = 0.404685642 hectares` (`AcreToHectareFactor`)
- **No intermediate rounding** - only the final displayed quantity is
  rounded (2 decimals, away from zero). `conversionApplied` tells the farmer
  whether the area was converted.
- **No incompatible unit conversion** - kg never becomes liters; the output
  unit (`requiredQuantityUnit`) is always exactly the dosage unit.
- Example (conversion): 2-hectare farm at 2 Kg/acre →
  `2 × 2.47105 = 4.9421 acres`, `4.9421 × 2 = 9.8842` → **9.88 Kg**.

## Validation & security

| Rule | Result |
| --- | --- |
| Missing/malformed JWT | `401` |
| Unknown farm id | `404` |
| Farm owned by someone else | `403` |
| Crop that belongs to another farm | `400` |
| Unknown crop | `404` |
| Dosage rate ≤ 0 or > 100,000 | `400` |
| Missing/whitespace input name or longer than 150 chars | `400` |
| Unsupported category / dosage unit / dosage basis | `400` |
| Farm without a positive recorded area | `400` |
| Farm stored in an unsupported size unit | `400` (never guessed) |

Domain exceptions are mapped by `GlobalExceptionMiddleware`; the controller
stays thin and try/catch-free like every other SABZ controller. The response
never exposes user ids, owner ids, emails, phone numbers, passwords, tokens
or API keys (verified by the test suite's response scan).

## Implementation notes

| Layer | Files |
| --- | --- |
| Domain | `Entities/InputCalculatorValues.cs` (controlled value sets) |
| Application | `DTOs/InputCalculator/*`, `Interfaces/IInputCalculatorService.cs`, `Services/InputCalculator/InputCalculatorService.cs` |
| API | `Controllers/InputCalculatorController.cs` |
| DI | Scoped `IInputCalculatorService` registration in `SABZ.Infrastructure/DependencyInjection.cs` |

The service reads through the existing `IFarmRepository` / `ICropRepository`
- no new repository, no DbContext writes anywhere on the path.

## Testing

`test-input-calculator.ps1` (64 checks, idempotent and self-cleaning, runs
twice with identical results) covers: authentication, basic arithmetic on
both unit systems, conversions in both directions against the documented
constants, canonical normalization, all validation rules (including a
zero-area farm flipped via SQL because the public farm endpoints refuse
non-positive sizes), farm ownership (200/403/404), crop-reference security,
a read-only guarantee (row counts across ten tables, table count 21,
migration count 11, seed farm untouched), and a response security scan.

## Limitations

- The calculator only multiplies; it cannot judge agronomic appropriateness.
- It supports exactly two area units (acres/hectares) and four quantity
  units (kg/liters/grams/milliliters); other units are rejected.
- Calculations are not stored, so there is no history or offline reuse.
- The optional `cropId` is context only and never influences the arithmetic.
