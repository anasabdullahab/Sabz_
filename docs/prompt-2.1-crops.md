# PROMPT 2.1 — Crop Catalog & Farm Crop Records

## Purpose

A reference crop catalog plus per-farm crop records so farmers can track what
they grow (and later feed rotation/recommendation features).

## Crop catalog

- `CropCatalog` table seeded via EF `HasData` in `SeedData.cs` — currently
  **22 crops** (20 original + `Mung bean` id 21 and `Mash bean` id 22 added by
  the PROMPT 4 migration).
- Fields: `Name`, `ScientificName`, `Category` (Cereal/Pulse/Vegetable/Fruit/
  Oilseed/Fiber/Cash Crop/Spice), `Description`.
- Catalog sources: FAO crop databases and PARC public crop profiles.

## Farm crop records (`Crop` entity)

| Method | Route | Description |
| --- | --- | --- |
| POST | `/api/farms/{farmId}/crops` | Add crop record to a farm |
| GET | `/api/farms/{farmId}/crops` | List farm crops |
| GET | `/api/crops/{id}` | Get one crop record |
| PUT | `/api/crops/{id}` | Update crop record |
| DELETE | `/api/crops/{id}` | Delete crop record |

- Fields: `CropName` (free text, optionally linked to `CropCatalogId`),
  `Season` (Rabi/Kharif), `Status`, `PlantingDate`, `GrowthStage`, `PreviousCrop`.
- Ownership enforced through the parent farm (JWT user must own the farm).
