# Prompt 8: Central In-App Notification & Reminder Foundation

## Purpose

A central, extensible in-app notification foundation. Farmers get reminders
inside the SABZ app; other features can attach notifications later by adding
rows with new category/reference values — no schema changes, no
switch-statement business rules.

**IN-APP ONLY.** Notifications are database rows retrieved through the API.
There is no SMS, email, WhatsApp, push notification, Firebase, or any external
delivery channel, and none is claimed.

## Hard rules honoured

- `UserId` is **never** accepted from clients. For monitoring reminders it is
  derived server-side: `CropMonitoringCheck -> Crop -> Farm -> Farm.UserId`.
- No background jobs, schedulers, queues, Hangfire/Quartz/cron. Due
  notifications are generated **lazily** when the app evaluates due checks
  (`GET /api/monitoring/due`), wrapped so notification failures can never
  break the monitoring read path or change monitoring state.
- Generation is **idempotent and concurrency-safe**, two-layered:
  1. Application-level pre-check on `(UserId, ReferenceType, ReferenceId, Category)`
     (one lookup per user per batch).
  2. Database unique index on `(UserId, Category, ReferenceType, ReferenceId)`;
     a unique-index conflict from a concurrent insert is caught, the pending row
     is detached and treated as "already exists".
- 100 calls to `GET /api/monitoring/due` create at most ONE `MonitoringDue`
  notification per monitoring check (verified by tests, including 6 parallel
  requests).
- Prompt 7 behaviour is untouched: checks still persist `Scheduled`/`Completed`/
  `Skipped`, Due/Upcoming stay computed against `ISystemClock` UTC, and no
  redundant `MonitoringCompleted` / `MonitoringSkipped` / `MonitoringUpcoming`
  notifications are generated.
- Crop creation stays uncoupled from notifications (`CropService` does not
  reference the notification service at all).

## Data model

`Notification` (`src/SABZ.Domain/Entities/Notification.cs`):

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | Primary key |
| `UserId` | `Guid` | FK -> `Users`, cascade delete; never client-supplied |
| `Title` | `string(200)` | e.g. "Crop monitoring check due" |
| `Message` | `string(1000)` | Farmer-facing reminder text |
| `Category` | `string(50)` | See categories below |
| `ReferenceType` | `string(50)` | e.g. `CropMonitoringCheck`, `None` |
| `ReferenceId` | `Guid` | `Guid.Empty` when no reference |
| `IsRead` | `bool` | Read state |
| `CreatedAt` | `DateTime` UTC | Defaults to `GETUTCDATE()` |
| `ReadAt` | `DateTime?` UTC | Set when marked read |

`ReferenceType`/`ReferenceId` are non-nullable (sentinels `None`/empty) so the
duplicate-prevention unique index has no nullable columns and behaves
deterministically.

### Categories (`NotificationCategories`)

`MonitoringDue`, `MonitoringUpcoming`, `MonitoringCompleted`,
`MonitoringSkipped`, `System`. Prompt 8 only ever creates **MonitoringDue**;
the others are documented vocabulary for future explicitly-requested workflows
(no fake future categories are produced today).

## Endpoints (all JWT-only, user-scoped)

| Method & route | Response |
| --- | --- |
| `GET /api/notifications?take=N` | User's notifications, newest first (default 50, max 100) |
| `GET /api/notifications/unread` | Unread notifications, newest first |
| `GET /api/notifications/unread-count` | `{ "count": 5 }` |
| `PATCH /api/notifications/{notificationId}/read` | Notification DTO; idempotent (re-marking keeps the original `readAt`) |
| `PATCH /api/notifications/read-all` | `{ "markedRead": N }` (0 on repeat) |

Security semantics: `401` without/with an invalid token, `404` for unknown
notification ids, `403` for another user's notifications (explicit IDOR
protection). The DTO exposes only `id, title, message, category, referenceType,
referenceId, isRead, createdAt, readAt` — never `userId`.

There is intentionally **no endpoint that creates notifications** or accepts a
UserId; creation happens exclusively through server-side workflows
(`INotificationService.EnsureDueNotificationsAsync`).

## Monitoring integration

`MonitoringService.GetDueChecksAsync` computes the due list exactly as in
Prompt 7, then calls `INotificationService.EnsureDueNotificationsAsync` inside a
try/catch: any notification failure is logged and swallowed, the due list is
still returned, and monitoring state is never modified. Time handling reuses
the existing `ISystemClock` singleton — no second clock was introduced.

## Migration

`20260821162324_AddNotifications` — purely additive:

- Creates `Notifications` (+ FK to `Users` with cascade delete).
- Indexes: `(UserId, CreatedAt)`, `(UserId, IsRead)`, and the unique
  `(UserId, Category, ReferenceType, ReferenceId)` duplicate-prevention index.
- `Down()` removes only the Prompt 8 table; Prompts 1–7 data verified
  unchanged before/after (row-count snapshots).

## Testing

`test-notifications.ps1` (untracked, idempotent, self-cleaning; 78 checks,
green twice in a row): lazy generation, duplicate prevention over repeated due
calls, 6-way parallel concurrency, auth 401 (missing/malformed token),
ownership 403/404, DTO hygiene (no `userId` in JSON), read-state lifecycle
(idempotent mark-read, read-all, unread count), no redundant
Completed/Skipped/Upcoming notifications, Prompt 7 regressions (due/upcoming/
complete/skip/generate), Prompt 6 regression (502 not-configured, no API key,
400 non-image), Prompt 4/5 regressions, crop CRUD with notifications
decoupled, and DB integrity guards (Ahmed Farm, seed counts, unique index).

## Components

| Component | Location |
| --- | --- |
| `Notification`, `NotificationCategories`, `ReferenceTypes` | `src/SABZ.Domain/Entities` |
| `INotificationRepository` / `INotificationService` | `src/SABZ.Application/Interfaces` |
| `NotificationDtos.cs` (`NotificationDto`, `UnreadCountResponseDto`, `MarkAllReadResponseDto`) | `src/SABZ.Application/DTOs/Notifications` |
| `NotificationService` | `src/SABZ.Application/Services/Notifications` |
| `NotificationRepository` | `src/SABZ.Infrastructure/Repositories` |
| `NotificationsController` | `src/SABZ.API/Controllers` |
| Due-notification hook | `MonitoringService.GetDueChecksAsync` |

## Deliberately NOT built

- Background jobs / schedulers / queues of any kind.
- SMS / email / WhatsApp / push / Firebase / any external delivery.
- Weather alerts, satellite alerts, or any alert claiming external data.
- `MonitoringUpcoming` / `MonitoringCompleted` / `MonitoringSkipped`
  notification generation (no deterministic, configurable requirement exists
  yet; focus stays on DUE).
- Any client-facing notification creation endpoint.
