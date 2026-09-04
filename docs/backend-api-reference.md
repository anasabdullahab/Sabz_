# SABZ Backend — Complete API & Architecture Reference

> **Purpose**: This document gives an AI (or developer) full understanding of the SABZ backend so it can design and build a matching frontend. No backend code inspection should be needed after reading this.

---

## 1. Application Overview

SABZ (meaning "green" in Urdu) is a **Smart Agriculture Platform** for Pakistani farmers. It provides:

- Farm & crop management
- AI-powered crop suitability and recommendations
- Weather intelligence
- AI-based plant disease detection (image upload)
- Voice-first AI agronomist assistant
- Crop monitoring with scheduled check-ins
- In-app notifications
- Financial ledger with health analytics
- Farm performance dashboards
- Farmer community (posts + comments)
- Equipment marketplace with private inbox messaging
- Precision input calculator
- Crop price intelligence (reference data)

**Target users**: Farmers in Pakistan. The UI should support English and Urdu (RTL).

---

## 2. Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10.0 (net10.0) |
| Web Framework | ASP.NET Core 10 |
| Database | SQL Server (LocalDB for dev) |
| ORM | Entity Framework Core 10 |
| Auth | JWT Bearer (24-hour tokens) |
| AI Text/Vision | DashScope/Qwen (qwen-vl-max, qwen-plus, qwen2-audio-instruct) |
| Weather | Open-Meteo API (free, no key) |
| Image Validation | SixLabors.ImageSharp 3.1 |
| API Docs | Swagger (Swashbuckle 10.2) |
| Password Hashing | ASP.NET Core PasswordHasher |

---

## 3. Project Structure (Clean Architecture)

```
src/
├── SABZ.API/                  ← Controllers, Middleware, Program.cs
├── SABZ.Application/          ← Services, DTOs, Interfaces
│   ├── DTOs/                  ← Request/Response objects per feature
│   ├── Interfaces/            ← Service + Repository contracts
│   └── Services/              ← Business logic implementations
├── SABZ.Domain/               ← Entities, Exceptions, Value Objects
│   ├── Entities/              ← Database models + static value classes
│   └── Exceptions/            ← Domain exception types
└── SABZ.Infrastructure/       ← Repositories, External Providers, DB
    ├── Persistence/           ← DbContext, SeedData, LocationDataSeeder
    ├── Repositories/          ← EF Core implementations
    ├── Services/              ← PasswordService, TokenService, SystemClock
    └── Services/{Feature}/    ← External provider implementations
```

---

## 4. Authentication

- **Registration**: POST `/api/auth/register` — does NOT return a JWT
- **Login**: POST `/api/auth/login` — returns a JWT token (24h expiry)
- **Get current user**: GET `/api/auth/me` — requires `[Authorize]` header
- **JWT claims**: `NameIdentifier` (user Guid), `Role`, `Email`
- **Ownership**: All services derive `userId` from JWT claims — never from request body
- **Public endpoints**: Auth (register/login) and Locations (provinces/districts/tehsils)

### Login Request
```json
{ "identifier": "email@example.com OR 03001234567", "password": "..." }
```
The `identifier` field accepts either email or phone number.

### Register Request
```json
{
  "fullName": "...",
  "email": "...",          // optional
  "phoneNumber": "...",    // optional — at least one of email/phone required
  "password": "...",       // required, min 8 chars
  "confirmPassword": "...",// required, must match password
  "preferredLanguage": "English" // optional, defaults to "English"
}
```

### Auth Response (both register and login)
```json
{
  "success": true,
  "message": "Registration successful." | "Login successful.",
  "token": "...",       // present only for login
  "user": { ... }       // UserResponse, present for both
}
```

### User Response
```json
{
  "id": "guid",
  "fullName": "...",
  "email": "...",
  "phoneNumber": "...",
  "preferredLanguage": "English",
  "role": "Farmer",
  "createdAt": "2026-01-01T00:00:00Z"
}
```

---

## 5. Error Handling (Global Exception Middleware)

All errors return JSON with `application/json` content type, serialized with **camelCase** naming.

| Exception | HTTP Status | Response Shape |
|-----------|------------|----------------|
| ValidationException | 400 Bad Request | `{ message, errors: { field: [messages] } }` |
| AuthenticationException | 401 Unauthorized | `{ message }` |
| ForbiddenException | 403 Forbidden | `{ message }` |
| NotFoundException | 404 Not Found | `{ message }` |
| ConflictException | 409 Conflict | `{ message }` |
| WeatherProviderException | 502 Bad Gateway | `{ message }` |
| DiseaseProviderException | 502 Bad Gateway | `{ message }` |
| AgronomistProviderException | 502 Bad Gateway | `{ message }` |
| CropPriceProviderException | 502 Bad Gateway | `{ message, code: "CropPriceProviderUnavailable" }` |
| Default (unexpected) | 500 Internal Server Error | `{ message: "An unexpected error occurred..." }` |

**DataAnnotations validation** runs in controllers before service calls. Validation errors use the `ValidationException` with an `errors` dictionary containing field-level messages.

---

## 6. Complete API Reference

### 6.1 Auth (`api/auth`) — Public
| Method | Route | Body | Returns | Auth |
|--------|-------|------|---------|------|
| POST | `/register` | RegisterRequest | AuthResponse | No |
| POST | `/login` | LoginRequest | AuthResponse | No |
| GET | `/me` | — | UserResponse | Yes |

### 6.2 Locations (`api/locations`) — Public
| Method | Route | Returns |
|--------|-------|---------|
| GET | `/provinces` | List\<LocationDto\> |
| GET | `/provinces/{provinceId:int}/districts` | List\<LocationDto\> |
| GET | `/districts/{districtId:int}/tehsils` | List\<LocationDto\> |

**LocationDto**: `{ id: int, name: string }`

### 6.3 Farms (`api/farms`) — All [Authorize]
| Method | Route | Body | Returns |
|--------|-------|------|---------|
| POST | `/` | CreateFarmDto | FarmResponseDto |
| GET | `/` | — | List\<FarmResponseDto\> |
| GET | `/{id:guid}` | — | FarmResponseDto |
| PUT | `/{id:guid}` | UpdateFarmDto | FarmResponseDto |
| DELETE | `/{id:guid}` | — | 204 NoContent |

**CreateFarmDto / UpdateFarmDto**:
- `farmName`: string [Required, MinLength(2)]
- `provinceId`: int? (nullable)
- `districtId`: int? (nullable)
- `tehsilId`: int? (nullable)
- `latitude`: decimal? [Range(-90, 90)]
- `longitude`: decimal? [Range(-180, 180)]
- `farmSize`: decimal [Range(0.01, max)]
- `farmSizeUnit`: string [Required] (default "Acres")
- `soilType`: string? (nullable)
- `irrigationType`: string? (nullable)

**FarmResponseDto**: all above + `id`, `provinceName`, `districtName`, `tehsilName`, `createdAt`, `updatedAt`

### 6.4 Crops — All [Authorize]
| Method | Route | Body | Returns |
|--------|-------|------|---------|
| POST | `api/farms/{farmId:guid}/crops` | CreateCropDto | CropResponseDto |
| GET | `api/farms/{farmId:guid}/crops` | — | List\<CropResponseDto\> |
| GET | `api/crops/{id:guid}` | — | CropResponseDto |
| PUT | `api/crops/{id:guid}` | UpdateCropDto | CropResponseDto |
| DELETE | `api/crops/{id:guid}` | — | 204 NoContent |

**CreateCropDto**:
- `cropName`: string [Required, MinLength(2)]
- `cropCatalogId`: int? (nullable)
- `season`: string [Required]
- `plantingDate`: DateTime? (nullable)
- `harvestDate`: DateTime? (nullable)
- `growthStage`: string?
- `previousCrop`: string?
- `status`: string?

**CropResponseDto**: all above + `id`, `farmId`, `createdAt`, `updatedAt`

### 6.5 Weather (`api/farms/{farmId:guid}/weather`) — All [Authorize]
| Method | Route | Returns |
|--------|-------|---------|
| GET | `/current` | WeatherResponseDto (with CurrentWeatherDto) |
| GET | `/forecast` | WeatherResponseDto (with ForecastDto) |

**WeatherResponseDto**: `{ farmId, farmName, current, forecast }`
**CurrentWeatherDto**: `{ temperature, apparentTemperature, humidity, precipitation, windSpeed, windDirection, weatherCode, isDay, time }`
**ForecastDto**: `{ daily: List<DailyForecastDto> }` — each has `{ date, tempMax, tempMin, precipitationSum, precipitationProbabilityMax, windSpeedMax, weatherCode }`

### 6.6 Crop Suitability (`api/farms/{farmId:guid}/crop-suitability`) — [Authorize]
| Method | Route | Query | Returns |
|--------|-------|-------|---------|
| GET | `/` | `season?` (string, auto-detected if null) | CropSuitabilityResponseDto |

Returns a list of all catalog crops scored for the farm's conditions with: suitability score, level, factor scores (location, climate, soil, water, season), positive factors, limitations, missing data.

### 6.7 Crop Recommendation (`api/farms/{farmId:guid}/crop-recommendations`) — [Authorize]
| Method | Route | Query | Returns |
|--------|-------|-------|---------|
| GET | `/` | `season?` (string, auto-detected if null) | CropRecommendationResponseDto |

Extends suitability with crop-history/rotation guidance. Includes `cropHistory` summary and per-crop `historyConsideration`.

### 6.8 Disease Detection (`api/farms/{farmId:guid}/disease-detection`) — [Authorize]
| Method | Route | Content-Type | Body | Returns |
|--------|-------|-------------|------|---------|
| POST | `/` | multipart/form-data | `image` (file), `cropId?` (form), `notes?` (form) | DiseaseDetectionResponseDto |

Upload a leaf photo → AI identifies disease → returns curated advice (symptoms, actions, prevention, monitoring).

### 6.9 Monitoring (`api/farms/{farmId:guid}/monitoring`) — [Authorize]
| Method | Route | Returns |
|--------|-------|---------|
| GET | `/crops/{cropId:guid}/checks` | List of MonitoringCheckDto |
| GET | `/crops/{cropId:guid}/checks/due` | Due checks only |
| GET | `/crops/{cropId:guid}/checks/upcoming` | Upcoming checks |
| PUT | `/checks/{checkId:guid}/complete` | MonitoringCompletionResponseDto |
| PUT | `/checks/{checkId:guid}/skip` | MonitoringCompletionResponseDto |

**MonitoringCheckDto**: `{ id, cropId, scheduledDate, status, title, description, inspectionItems, priority, observation, farmerNotes, completedAt, skippedAt }`

Complete request: `{ observation: "Normal"|"SomethingSuspicious", farmerNotes?: string }`
Skip request: `{ reason?: string }`

### 6.10 Notifications (`api/notifications`) — [Authorize]
| Method | Route | Returns |
|--------|-------|---------|
| GET | `/` | List\<NotificationDto\> |
| GET | `/unread` | Unread only |
| PUT | `/{id:guid}/read` | 200 OK |

**NotificationDto**: `{ id, title, message, category, referenceType, referenceId, isRead, createdAt, readAt }`

### 6.11 Financial Transactions — [Authorize]
| Method | Route | Body | Returns |
|--------|-------|------|---------|
| POST | `api/farms/{farmId:guid}/transactions` | CreateFinancialTransactionDto | FinancialTransactionResponseDto |
| GET | `api/farms/{farmId:guid}/transactions` | query: type?, category?, cropId?, fromDate?, toDate?, take? | List\<FinancialTransactionResponseDto\> |
| GET | `api/transactions/{id:guid}` | — | FinancialTransactionResponseDto |
| PUT | `api/transactions/{id:guid}` | UpdateFinancialTransactionDto | FinancialTransactionResponseDto |
| DELETE | `api/transactions/{id:guid}` | — | 204 NoContent |
| GET | `api/farms/{farmId:guid}/financial-summary` | query: cropId?, fromDate?, toDate? | FinancialSummaryResponseDto |

**CreateFinancialTransactionDto**:
- `transactionType`: string [Required] — "Income" or "Expense"
- `category`: string [Required] — from TransactionCategories set
- `amount`: decimal [Required, Range(0.01, max)]
- `transactionDate`: DateTime [Required]
- `cropId`: Guid? (nullable)
- `notes`: string? [MaxLength(1000)]

**Transaction categories**: Seeds, Fertilizer, Pesticide, Herbicide, Fungicide, Insecticide, Labor, Equipment, Irrigation, Fuel, Transport, Land, HarvestSale, Subsidy, Livestock, Other

### 6.12 Financial Health (`api/farms/{farmId:guid}/financial-health`) — [Authorize]
| Method | Route | Query | Returns |
|--------|-------|-------|---------|
| GET | `/` | fromDate?, toDate? | FinancialHealthSummaryDto |
| GET | `/categories` | fromDate?, toDate? | List\<CategoryBreakdownDto\> |
| GET | `/activity` | fromDate?, toDate? | FinancialActivityDto |
| GET | `/completeness` | — | FinancialCompletenessDto |
| GET | `/crops/{cropId:guid}/financial-health` | fromDate?, toDate? | FinancialHealthSummaryDto (crop-level) |

### 6.13 Farm Performance (`api/farms/{farmId:guid}/performance`) — [Authorize]
| Method | Route | Query | Returns |
|--------|-------|-------|---------|
| GET | `/` | fromDate?, toDate? | FarmPerformanceSummaryDto |
| GET | `/crops` | fromDate?, toDate? | List\<CropPerformanceDto\> |
| GET | `/activity` | — | FarmActivitySummaryDto |

### 6.14 Farm Dashboard (`api/farms/{farmId:guid}/dashboard`) — [Authorize]
| Method | Route | Returns |
|--------|-------|---------|
| GET | `/` | FarmDashboardDto (aggregated view) |

Aggregates: farm info, crops, monitoring checks, notifications, financial summary, performance, weather — all in one call.

### 6.15 Community — [Authorize]
| Method | Route | Body | Returns |
|--------|-------|------|---------|
| GET | `api/community/posts?page=&pageSize=` | — | PagedResult\<CommunityPostResponseDto\> |
| POST | `api/community/posts` | CreateCommunityPostDto | CommunityPostResponseDto |
| GET | `api/community/posts/{postId:guid}` | — | CommunityPostDetailDto |
| DELETE | `api/community/posts/{postId:guid}` | — | 204 NoContent |
| GET | `api/community/posts/{postId:guid}/comments?page=&pageSize=` | — | PagedResult\<CommunityCommentResponseDto\> |
| POST | `api/community/posts/{postId:guid}/comments` | CreateCommunityCommentDto | CommunityCommentResponseDto |
| DELETE | `api/community/comments/{commentId:guid}` | — | 204 NoContent |

**CreateCommunityPostDto**: `{ content: string [Required, MaxLength(2000)], imageUrl?: string [MaxLength(2048)] }`
**CreateCommunityCommentDto**: `{ content: string [Required, MaxLength(1000)] }`

### 6.16 Marketplace — [Authorize]
| Method | Route | Body | Returns |
|--------|-------|------|---------|
| GET | `api/marketplace/listings?page=&pageSize=&search=&category=&listingType=&location=&condition=` | — | MarketplacePagedResultDto |
| POST | `api/marketplace/listings` | CreateMarketplaceListingDto | MarketplaceListingResponseDto |
| GET | `api/marketplace/listings/{listingId:guid}` | — | MarketplaceListingResponseDto |
| PUT | `api/marketplace/listings/{listingId:guid}` | UpdateMarketplaceListingDto | MarketplaceListingResponseDto |
| DELETE | `api/marketplace/listings/{listingId:guid}` | — | 204 NoContent |

**CreateMarketplaceListingDto**:
- `title`: string [Required, MaxLength(150)]
- `category`: string [Required]
- `listingType`: string [Required] — "Sale" or "Rent"
- `description`: string [Required, MaxLength(2000)]
- `price`: decimal [Required, Range(0, max)]
- `priceUnit`: string [Required] — "Total", "Day", "Hour", "Week", "Month"
- `location`: string [Required, MaxLength(200)]
- `contactNumber`: string [Required, MaxLength(30)]
- `condition`: string [Required] — "New" or "Used"
- `availability`: string [Required, MaxLength(100)]
- `imageUrl`: string? [MaxLength(2048)]

### 6.17 Marketplace Inbox — [Authorize]
| Method | Route | Body | Returns |
|--------|-------|------|---------|
| GET | `api/marketplace/inbox?page=&pageSize=` | — | Paged inbox list |
| GET | `api/marketplace/inbox/{conversationId:guid}?page=&pageSize=` | — | Conversation with messages |
| POST | `api/marketplace/listings/{listingId:guid}/contact` | StartMarketplaceConversationDto | Conversation |
| POST | `api/marketplace/inbox/{conversationId:guid}/messages` | SendMarketplaceMessageDto | Message |

**StartMarketplaceConversationDto**: `{ message: string [Required, MaxLength(2000)] }`
**SendMarketplaceMessageDto**: `{ content: string [Required, MaxLength(2000)] }`

### 6.18 Agronomist Assistant (`api/farms/{farmId:guid}/agronomist`) — [Authorize]
| Method | Route | Content-Type | Body | Returns |
|--------|-------|-------------|------|---------|
| POST | `/chat` | application/json | TextAgronomistQuestionDto | AgronomistResponseDto |
| POST | `/voice` | multipart/form-data | `audio` (file) | VoiceAgronomistResponseDto |

**TextAgronomistQuestionDto**: `{ message: string }`
**AgronomistResponseDto**: `{ question, answer, language, farmContextUsed, limitations, disclaimer, generatedAt }`
**VoiceAgronomistResponseDto**: extends AgronomistResponseDto + `{ transcription, transcriptionProvider }`

Chat is READ-ONLY — never creates/modifies any data. No chat history persisted.

### 6.19 Input Calculator (`api/farms/{farmId:guid}/input-calculator`) — [Authorize]
| Method | Route | Body | Returns |
|--------|-------|------|---------|
| POST | `/` | InputCalculatorRequestDto | InputCalculatorResponseDto |

**Request**: `{ category, dosageRate, dosageBasis, quantityUnit, areaUnit }`
- `category`: "Fertilizer", "Pesticide", "Herbicide", "Fungicide", "Insecticide", "Other"
- `dosageBasis`: "PerAcre" or "PerHectare"
- `quantityUnit`: "Kg", "Liters", "Grams", "Milliliters"
- `areaUnit`: "Acres" or "Hectares"

Pure arithmetic — farm area is read from the DB, not from the request.

### 6.20 Crop Prices (`api/crop-prices`) — [Authorize]
| Method | Route | Query | Returns |
|--------|-------|-------|---------|
| GET | `/` | crop?, province?, district?, market?, fromDate?, toDate?, page?, pageSize? | CropPricePagedResultDto |
| GET | `/{cropName}` | fromDate?, toDate? | CropPriceDetailDto |

**Currently returns REFERENCE DATA ONLY** (source: "SABZ Reference Dataset", dataStatus: "Reference"). Real AMIS Punjab integration not yet implemented.

---

## 7. Database Entities & Relationships

### Core Entities
```
User (Guid Id)
 ├── Farm (Guid Id) ──── many per user
 │    ├── Crop (Guid Id) ──── many per farm
 │    │    ├── CropMonitoringCheck ──── many per crop
 │    │    └── FinancialTransaction ──── optional per crop
 │    ├── FinancialTransaction ──── many per farm
 │    └── Notification ──── via check→crop→farm→user chain
 │
 ├── CommunityPost ──── many per user (soft-deletable)
 │    └── CommunityComment ──── many per post (soft-deletable)
 │
 ├── MarketplaceListing ──── many per user (soft-deletable)
 │    └── MarketplaceConversation ──── per listing/buyer/seller
 │         └── MarketplaceMessage ──── many per conversation
 │
 └── Notification ──── many per user

Province → District → Tehsil (Pakistan admin hierarchy)
CropCatalog → CropRequirement, RegionalCropSuitability, CropChangeRule, DiseaseInformation, CropMonitoringRule
```

### Key Relationships
- Farm belongs to Province, District, Tehsil (location hierarchy)
- Crop optionally links to CropCatalog (for suitability/recommendation data)
- FinancialTransaction links to Farm (required) and Crop (optional)
- MarketplaceConversation has unique constraint: (ListingId, BuyerUserId, SellerUserId)
- Soft-delete used for: CommunityPost, CommunityComment, MarketplaceListing, MarketplaceMessage

### Seed Data (22 crops in catalog)
Wheat, Rice, Cotton, Sugarcane, Maize, Millet, Sorghum, Chickpea, Lentil, Mung Bean, Mustard, Sunflower, Potato, Tomato, Onion, Chili Pepper, Okra, Cucumber, Mango, Citrus, Banana, Tobacco

---

## 8. External Integrations

| Integration | Provider | Purpose | Status |
|-------------|----------|---------|--------|
| Weather | Open-Meteo API | Current + 7-day forecast | **LIVE** (no API key needed) |
| Disease Detection | DashScope/Qwen qwen-vl-max | Image-based plant disease ID | **LIVE** (needs API key in config) |
| AI Agronomist | DashScope/Qwen qwen-plus | Text Q&A about farming | **LIVE** (needs API key in config) |
| Speech-to-Text | DashScope/Qwen qwen2-audio-instruct | Voice question transcription | **LIVE** (needs API key in config) |
| Crop Prices | ReferenceCropPriceProvider | Hardcoded reference data | **NOT LIVE** (placeholder) |

---

## 9. Configuration Sections (appsettings.json)

| Section | Purpose |
|---------|---------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` | JWT signing configuration |
| `Weather` | Open-Meteo base URL, timeout, caching durations |
| `CropSuitability` | Scoring weights (location 25%, climate 25%, soil 20%, water 15%, season 15%), thresholds, Kharif months |
| `CropRecommendation` | Caution/negative adjustment levels for crop-change rules |
| `DiseaseDetection` | DashScope API base URL + key, model name, image validation thresholds, confidence levels |
| `Agronomist` | Chat/STT model names, max question length, audio size limits, allowed audio types |

---

## 10. Important Backend Behaviors

1. **Ownership enforcement**: userId always from JWT. Trying to access another user's farm/crop/etc. returns 403 Forbidden.
2. **Soft deletes**: Community posts, comments, marketplace listings/messages use `IsDeleted` flag. Queries exclude soft-deleted rows.
3. **Pagination**: Community feed, marketplace listings, inbox, crop prices all use server-side pagination with `page` and `pageSize` query params.
4. **Caching**: Weather data is cached in memory (15 min for current, 60 min for forecast).
5. **Auto-detected season**: If `season` query param is omitted on suitability/recommendation endpoints, it defaults to "Kharif" (Apr–Sep) or "Rabi" (Oct–Mar) based on current month.
6. **Monitoring checks**: Generated from CropMonitoringRules when a crop is created. Checks have Scheduled/Completed/Skipped status lifecycle.
7. **Notifications**: Generated lazily from the monitoring read path (when fetching due checks). In-app only — no SMS/email/push.
8. **Financial amounts**: Always `decimal` with precision(18,2). Never floating-point.
9. **Marketplace**: Discovery/connection only. SABZ never processes payments. Listing types: Sale/Rent. Conditions: New/Used.
10. **Agronomist**: Read-only. Never creates/modifies farms, crops, transactions, or any data. Never persists chat history.

---

## 11. Remaining / Incomplete Backend Work

### Definitely Incomplete
1. **Crop Prices**: `ReferenceCropPriceProvider` returns hardcoded data labeled "NON-LIVE". Real AMIS Punjab integration needed.
2. **CORS**: No CORS policy configured. Frontend must use Vite proxy or backend needs CORS middleware added.
3. **Email/SMS/Push**: Notifications are in-app only. No email, SMS, or push notification integration.
4. **Background Jobs**: No scheduled tasks or background workers. Notifications are generated lazily on read.

### Partially Implemented / Future Considerations
5. **Monitoring trigger types**: `WeatherEvent` and `SatelliteAlert` trigger types are defined but never used — only `Scheduled` is active.
6. **Rate limiting**: No API rate limiting configured.
7. **API versioning**: No versioning strategy.
8. **Unit tests**: No test project visible in the solution.
9. **Logging**: Only exception-level logging in the middleware. No structured logging or request/response logging.
10. **File upload storage**: Disease detection and agronomist voice accept file uploads but don't persist them (processed in-memory only).
11. **Marketplace notifications**: Creating a listing or receiving a message doesn't generate notifications.
12. **Community moderation**: No AI moderation or reporting system for community content.
13. **User profile update**: No endpoint to update user profile (name, language, etc.) after registration.
14. **Password change/reset**: No password change or forgot-password endpoints.
15. **Farm ownership transfer**: No mechanism to transfer farm ownership.
16. **Image upload for community/marketplace**: These entities have `ImageUrl` fields but no file upload endpoint — the URL is provided directly in the request body.

---

## 12. Frontend Integration Notes

### What the frontend must handle
- **Login**: Two separate fields (Email / Phone Number) with OR divider. Exactly one required. Send as `{ identifier, password }`.
- **Registration**: Email and/or phone (at least one required). Redirect to login after success — no auto-login.
- **JWT**: Store in localStorage, attach as `Authorization: Bearer <token>` header.
- **401 handling**: On 401 response, clear token and redirect to login.
- **Error display**: Parse `{ message, errors? }` response. Show `message` prominently. Append field errors from `errors` dictionary when present.
- **File uploads**: Disease detection uses `multipart/form-data`. Agronomist voice uses `multipart/form-data`.
- **Pagination**: Community, marketplace, inbox, crop prices all use `?page=1&pageSize=20` query params.
- **Location picker**: Farm creation needs province → district → tehsil cascading dropdowns (public API).
- **Seasons**: Pakistan has two agricultural seasons — Kharif (Apr–Sep) and Rabi (Oct–Mar).

### Suggested Frontend Pages/Features
1. **Auth**: Login, Register
2. **Dashboard**: Farm overview with monitoring alerts, weather, financial summary
3. **Farms**: CRUD with location picker
4. **Crops**: CRUD within farms, with monitoring check integration
5. **Weather**: Current conditions + 7-day forecast per farm
6. **Crop Suitability**: Scored crop list for a farm
7. **Crop Recommendation**: Suitability + rotation guidance
8. **Disease Detection**: Image upload → AI result with advice
9. **Monitoring**: Check list per crop, complete/skip workflow
10. **Notifications**: Feed with read/unread status
11. **Financial**: Transaction ledger + summary + health analytics
12. **Performance**: Farm-level + crop-level + activity analytics
13. **Community**: Post feed, create post, comments, delete
14. **Marketplace**: Listings with search/filter, create listing, detail view
15. **Marketplace Inbox**: Conversation list, message thread, contact seller
16. **Agronomist**: Text chat + voice input with farm context
17. **Input Calculator**: Dosage calculation form
18. **Crop Prices**: Filterable price table with disclaimer
19. **Settings**: User profile (language preference)
