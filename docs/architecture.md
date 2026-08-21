# SABZ Backend Architecture

## Solution layout (Clean Architecture)

The solution has four projects; there is no `.sln` file, builds run against
the API project which references everything else:

```
src/
  SABZ.Domain          Entities, domain exceptions. No dependencies.
  SABZ.Application     DTOs, interfaces, services (business logic). Depends on Domain.
  SABZ.Infrastructure  EF Core DbContext, migrations, seed data, repositories,
                       external providers (Open-Meteo). Implements Application interfaces.
  SABZ.API             ASP.NET Core Web API, controllers, middleware, configuration.
```

Dependency direction is strictly inward: API -> Infrastructure -> Application -> Domain.

## Key conventions

- **Services in Application, providers in Infrastructure.** Business logic
  (auth, farms, crops, weather service, crop suitability) lives in
  `SABZ.Application/Services/*`; anything touching EF Core, files, or external
  HTTP APIs lives in Infrastructure.
- **Ownership checks are server-side.** User identity comes from the JWT claim
  `ClaimTypes.NameIdentifier` only; clients never send a user id in request bodies.
- **Domain exceptions -> HTTP status codes** via centralized exception
  handling middleware:
  - `ValidationException` -> 400
  - `AuthenticationException` -> 401
  - `ForbiddenException` -> 403
  - `NotFoundException` -> 404
  - `WeatherProviderException` -> 502
- **Configuration via `IOptions<T>`** bound from `appsettings.json` sections
  (`Jwt`, `Weather`, `CropSuitability`) and registered in
  `SABZ.Infrastructure/DependencyInjection.cs`.
- **Database:** SQL Server LocalDB (`(localdb)\mssqllocaldb`, database `SabzDB`),
  EF Core migrations, `HasData` seeding for reference datasets, runtime seeding
  of administrative divisions by `LocationDataSeeder` from an embedded JSON resource.

## Cross-cutting capabilities

| Capability | Implementation |
| --- | --- |
| Authentication | JWT bearer (`SABZ.Application/Services/Auth`) |
| Weather data | Open-Meteo via named HttpClient + `IMemoryCache` (15 min current / 60 min forecast) |
| Crop suitability | Data-driven scoring engine over `CropRequirement` + `RegionalCropSuitability` reference data |
| Error handling | Exception middleware + problem-style JSON errors |
