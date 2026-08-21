# PROMPT 2 — Location Data & Farm Management Foundation

## Purpose

Pakistan administrative hierarchy (Province -> District -> Tehsil) and farm
CRUD bound to that hierarchy, with optional GPS coordinates.

## Administrative data

- Seeded at **runtime** by `LocationDataSeeder` from the embedded resource
  `SeedData/pakistan-admin-data.json` (idempotent: inserts only missing rows).
- 7 provinces, full district and tehsil dataset. See
  [pakistan-admin-data.md](pakistan-admin-data.md) for dataset details.
- Read-only endpoints (`api/locations`):
  - `GET /api/locations/provinces`
  - `GET /api/locations/provinces/{provinceId}/districts`
  - `GET /api/locations/districts/{districtId}/tehsils`

## Farm CRUD (`api/farms`, all authenticated)

| Method | Route | Description |
| --- | --- | --- |
| POST | `/api/farms` | Create farm (owner = JWT user) |
| GET | `/api/farms` | List own farms |
| GET | `/api/farms/{id}` | Get one (owner only) |
| PUT | `/api/farms/{id}` | Update (owner only) |
| DELETE | `/api/farms/{id}` | Delete (owner only) |

## Farm model

- `FarmName`, `FarmSize` + `FarmSizeUnit`, location hierarchy
  (`ProvinceId`, `DistrictId`, `TehsilId`), optional `Latitude`/`Longitude`
  (decimal, validated ranges), optional `SoilType` and `IrrigationType`
  (free-text).
- Ownership: `UserId` FK; all reads/writes verify `farm.UserId == JWT user`.
- Deleting a farm cascades its crop records; location tables are protected by
  `Restrict` delete behavior.
