# SABZ — Pakistan Administrative Reference Data

## Dataset

**Pakistan Administrative Divisions**

This application uses the Pakistan Administrative Divisions open dataset as its
initial administrative reference dataset.

- **Source:** <https://github.com/open-admin-data/pakistan-administrative-divisions>
- **License:** [CC-BY-4.0](https://creativecommons.org/licenses/by/4.0/)
- **Dataset update date:** June 1, 2026

## Scope

The dataset provides the complete administrative hierarchy used by SABZ:

```
Province / Territory
    └── District
        └── Tehsil
```

### Current import statistics

| Level      | Count |
|------------|-------|
| Provinces  |     7 |
| Districts  |   160 |
| Tehsils    |   577 |

### Provinces / Territories

| # | Name                    | Districts |
|---|-------------------------|-----------|
| 1 | Punjab                  |        36 |
| 2 | Azad Kashmir            |        10 |
| 3 | Balochistan             |        35 |
| 4 | Gilgit Baltistan        |        14 |
| 5 | Islamabad               |         1 |
| 6 | Khyber Pakhtunkhwa      |        35 |
| 7 | Sindh                   |        29 |

## How the data is used

The dataset is embedded as a JSON resource
(`SABZ.Infrastructure/SeedData/pakistan-admin-data.json`) and loaded at
application startup by `LocationDataSeeder`. The seeder is idempotent:
running it again will not create duplicate records.

The data populates the following database tables:

- `Provinces`
- `Districts` (FK → Provinces)
- `Tehsils` (FK → Districts)

These tables are referenced by the `Farms` table for location data.

## Important notice

This dataset is **not** an official Pakistan government dataset. It is a
community-maintained open dataset licensed under CC-BY-4.0. Administrative
boundaries and names may not reflect the latest official government
classifications.
