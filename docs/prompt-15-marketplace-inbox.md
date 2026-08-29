# Prompt 15: Farmer Marketplace + Private Inbox Foundation

## Purpose

A farmer-to-farmer marketplace for agricultural equipment (tractors,
harvesters, sprayers, tillers, ...) where authenticated SABZ farmers can
**Discover → View → Contact → Arrange**: browse sale/rent listings, open a
listing, message the seller through a private inbox, and arrange any deal
directly between themselves outside SABZ.

> **The SABZ Farmer Marketplace is a farmer-to-farmer discovery and
> communication feature. SABZ does not process payments, financial
> transactions, banking, loans, credit, insurance, or investment activity.
> Marketplace prices are informational and any sale or rental arrangement is
> handled directly between users outside SABZ.**

> **Marketplace messages represent communication between individual users and
> are not guaranteed to constitute verified agricultural, commercial, legal,
> or financial advice.**

The feature is deliberately a **connection/discovery foundation, not a
commerce platform**: no payments, wallets, orders, escrow, commissions or
delivery; no ratings/reviews, likes/follows, AI moderation or marketplace
notifications; no public message feed. Listing activity never creates a
`FinancialTransaction` row (verified by the test suite).

## User flow

1. A farmer logs in (`POST /api/auth/login`) and browses the newest-first
   marketplace feed (`GET /api/marketplace/listings`), optionally filtering
   by search text, category, listing type, location or condition.
2. The farmer opens a listing (`GET /api/marketplace/listings/{listingId}`)
   for full details. The seller's contact number is never shown here to
   non-owners.
3. The farmer contacts the seller
   (`POST /api/marketplace/listings/{listingId}/contact`) with a first
   message. This opens (or reuses) a private conversation.
4. Both participants exchange messages through the private inbox
   (`GET /api/marketplace/inbox`, `GET /api/marketplace/inbox/{conversationId}`,
   `POST /api/marketplace/inbox/{conversationId}/messages`).
5. The buyer and seller arrange inspection, price agreement and hand-over
   directly, outside SABZ. The seller's contact number is visible to the
   owner on their own listing detail.

## Domain model

| Entity | Properties | Notes |
| --- | --- | --- |
| `MarketplaceListing` | `Id`, `UserId`, `Title` (≤ 150), `Category` (≤ 50), `ListingType` (Sale/Rent), `Description` (≤ 2000), `Price` decimal(18,2), `PriceUnit` (Total/Day/Hour/Week/Month), `Location` (≤ 200), `ContactNumber` (≤ 30), `Condition` (New/Used), `Availability` (≤ 100), `ImageUrl` (nullable, ≤ 2048), `CreatedAt`, `UpdatedAt?`, `IsDeleted`, `DeletedAt?` | One row per equipment offer; `UserId` always JWT-derived |
| `MarketplaceConversation` | `Id`, `ListingId`, `BuyerUserId`, `SellerUserId`, `CreatedAt`, `UpdatedAt`, `IsDeleted` | Unique per `(ListingId, BuyerUserId, SellerUserId)` trio - "Message Seller" never duplicates a thread |
| `MarketplaceMessage` | `Id`, `ConversationId`, `SenderUserId`, `Content` (≤ 2000), `CreatedAt`, `IsDeleted` | Append-only message inside a conversation |

Controlled values live in `MarketplaceValues` (same convention as
`TransactionCategories`): case-insensitive normalisation to the canonical
value, invalid values rejected with `400`.

Foreign keys: `MarketplaceListings.UserId → Users (Restrict)`,
`MarketplaceConversations.ListingId → MarketplaceListings (Restrict)`,
`MarketplaceConversations.BuyerUserId/SellerUserId → Users (Restrict)`,
`MarketplaceMessages.ConversationId → MarketplaceConversations (Cascade)`,
`MarketplaceMessages.SenderUserId → Users (Restrict)`. Exactly one cascade
path keeps SQL Server constraint 1785 satisfied, and **deleting a listing
never deletes message history** - conversations stay readable for both
participants with the listing title preserved.

Soft delete is enforced by explicit `IsDeleted` filters in repository
queries, consistent with the rest of the codebase.

## Endpoints

All nine endpoints require `[Authorize]` (authenticated reads are consistent
with the rest of SABZ). Controllers are thin; business rules live in
`MarketplaceService` / `MarketplaceInboxService`, persistence in
`MarketplaceListingRepository`, `MarketplaceConversationRepository` and
`MarketplaceMessageRepository`.

| Route | Behaviour |
| --- | --- |
| `GET /api/marketplace/listings?page=1&pageSize=20&search=&category=&listingType=&location=&condition=` | DB-side paginated feed, newest first (`CreatedAt DESC, Id DESC`); `pageSize` max 50, invalid page/pageSize → `400`; search matches title (wildcards escaped); **never includes contact numbers** |
| `POST /api/marketplace/listings` | Create a listing; the JWT user becomes the owner |
| `GET /api/marketplace/listings/{listingId}` | Listing detail; `contactNumber` is returned **only when the caller owns the listing** |
| `PUT /api/marketplace/listings/{listingId}` | Owner-only full update (`403` otherwise); ownership cannot change |
| `DELETE /api/marketplace/listings/{listingId}` | Owner-only soft delete (`204`); inbox history stays intact; listing disappears from the feed |
| `GET /api/marketplace/inbox?page=1&pageSize=50` | Caller's conversations, newest activity first (`UpdatedAt DESC, Id DESC`), with counter-party display name, role (`Buyer`/`Seller`) and latest-message preview |
| `GET /api/marketplace/inbox/{conversationId}?page=1&pageSize=50` | Conversation detail with DB-side paginated messages, oldest first; participants only (`403`), unknown → `404` |
| `POST /api/marketplace/listings/{listingId}/contact` | Start (or reuse) a conversation with the seller and send the first message; sellers cannot contact their own listing (`400`) |
| `POST /api/marketplace/inbox/{conversationId}/messages` | Participants-only message send; returns the created message with `isOwnMessage = true` |

Listing feed and inbox/message pages use the small reusable `PagedResult<T>`
envelope: `{ items, page, pageSize, totalCount, totalPages }`
(`MarketplacePagedResultDto` / `MarketplaceInboxPagedResultDto`).

## Authorization and privacy

- Identity is always `ClaimTypes.NameIdentifier` from the JWT. No DTO
  accepts a client-supplied owner/buyer/seller/sender id; spoofing is
  impossible by design (verified by tests that send fake ids in bodies).
- Conversation access: the JWT user must be the `BuyerUserId` or the
  `SellerUserId`; non-participants get a consistent `403`.
- Updating/deleting another farmer's listing → `403`; unknown or deleted
  listing/conversation → `404`; no token → `401`; validation → `400` via the
  existing `GlobalExceptionMiddleware` (controllers contain no try/catch).
- The seller **contact number is private to the owner**: it appears only in
  the owner's own listing detail and is absent from the feed, other users'
  detail views and every inbox payload.
- Responses expose only display names (`User.FullName`), roles and a
  server-computed `isOwnedByCurrentUser` flag. Never `UserId`, email, phone
  (except the owner's own), password hash, token or key material.

## Validation rules

- `Title`: required, non-whitespace, ≤ **150** characters.
- `Category`: required, non-whitespace, ≤ **50** characters.
- `ListingType`: only `Sale` / `Rent` (case-insensitive, normalised).
- `Description`: required, non-whitespace, ≤ **2000** characters.
- `Price`: > 0 and ≤ **1,000,000,000**, stored as `decimal(18,2)`.
- `PriceUnit`: controlled values `Total` / `Day` / `Hour` / `Week` / `Month`.
- `Location`: required, non-whitespace, ≤ **200** characters.
- `ContactNumber`: required, permissive phone-like pattern for Pakistani
  formats (`+92 300 1234567`, `0300-1234567`, ...), 7–30 chars, 7–15 digits.
- `Condition`: only `New` / `Used`.
- `Availability`: required, non-whitespace, ≤ **100** characters.
- `ImageUrl`: optional absolute `http://`/`https://` URL ≤ 2048 chars
  (same validation as Prompt 14; local paths rejected).
- Messages: required, non-whitespace, ≤ **2000** characters.
- `page ≥ 1`, `1 ≤ pageSize ≤ 50`; anything else → `400`.

## Database changes

One additive migration, `20260829075103_AddMarketplaceAndInbox`:

- New tables: `MarketplaceListings`, `MarketplaceConversations`,
  `MarketplaceMessages` (table count 18 → 21, migrations 10 → 11).
- Indexes include `IX_MarketplaceListings_UserId_CreatedAt`,
  `IX_MarketplaceListings_CreatedAt`, `IX_MarketplaceListings_Category`,
  `IX_MarketplaceListings_ListingType`,
  `IX_MarketplaceConversations_BuyerUserId_UpdatedAt`,
  `IX_MarketplaceConversations_SellerUserId_UpdatedAt`,
  `IX_MarketplaceConversations_ListingId`, a **unique**
  `(ListingId, BuyerUserId, SellerUserId)` index,
  `IX_MarketplaceMessages_ConversationId_CreatedAt` and
  `IX_MarketplaceMessages_SenderUserId`.
- `CreatedAt`/`UpdatedAt` default to `GETUTCDATE()`; `Price` is
  `decimal(18,2)`.
- No Prompt 1–14 table was altered. `dotnet ef migrations
  has-pending-model-changes` reports no pending changes after applying;
  `DBCC CHECKDB` is clean.

## Performance

- All reads are `AsNoTracking()` SQL projections into read models - no
  entities on read paths, no N+1: buyer/seller/seller display names,
  ownership flags and the latest-message preview are computed in SQL in one
  round-trip per page.
- Pagination is DB-side (`OFFSET/FETCH`) for the listing feed, the inbox and
  message threads; nothing is ever loaded into memory.
- Ordering is deterministic: listings `CreatedAt DESC, Id DESC`, inbox
  `UpdatedAt DESC, Id DESC`, messages `CreatedAt ASC, Id ASC`.
- Search wildcards (`%`, `_`, `[`) are escaped before `LIKE`.
- Soft-deleted rows are excluded from every normal query.

## Limitations

- Listings cannot be marked sold/reserved; farmers edit or delete instead.
- No image upload pipeline - `ImageUrl` is a safe reference only.
- No read receipts, typing indicators, attachments or message editing.
- No marketplace notifications (Prompt 8 is untouched) and no background jobs.
- Marketplace content is user-generated and unverified (see the statements at
  the top of this document).

## Deliberate exclusions

Payments, wallets, checkout, orders, escrow, commissions, refunds, delivery
or logistics; any `FinancialTransaction` for marketplace activity; banking,
loans, credit, insurance or investment features; ratings/reviews or trust
scores; likes/followers; public messaging or message feeds; AI moderation,
AI pricing or AI recommendations; background jobs (Hangfire/Quartz);
marketplace notifications. No React frontend was built; the API is
Swagger-documented for future clients.

## Testing

`test-marketplace.ps1` (intentionally untracked, idempotent) covers 63 checks
with fixture users `test21@example.com` (seller), `userb3@example.com`
(buyer), `userc3@example.com` (unrelated farmer) and an `MK ` listing prefix
cleaned through the public API before and after each run:

- Authentication (1.1–1.9): every marketplace/inbox endpoint rejects missing
  tokens with `401`.
- Creation & validation (2.1–2.19): sale/rent creation plus rejection of
  missing/whitespace/oversized fields, bad listing type, zero/negative/
  excessive price, invalid condition and unsafe image URLs.
- Ownership (3.1–3.5): owner update/delete, foreign update/delete `403`,
  deleted listings vanish from feed and detail.
- Search/filter/pagination (4.1–4.8): title search, category/listingType/
  location/condition filters, page size limits, invalid pagination `400`.
- Inbox (5.1–5.8): contact starts a conversation, duplicate contact reuses
  it, both parties see correct roles/names, messaging works both ways,
  message pagination, newest-activity ordering with latest-message preview.
- Conversation security (6.1–6.6): non-participant read/send `403`,
  sender/buyer/seller spoofing impossible, seller cannot contact own listing.
- Data leakage (7.1–7.2): raw JSON never exposes ids, email, password, token
  or keys; the seller contact number is absent from the public feed.
- Financial isolation (8.1): marketplace activity creates zero
  `FinancialTransactions`.
- Persistence (9.1–9.5): listings/messages persist, soft delete recorded in
  the DB, conversations survive listing deletion, no orphans, no duplicate
  conversation identities.

The suite was run twice consecutively with 63/63 passes. Prompt 1–14
regression suites were re-run afterwards (schema baselines legitimately moved
18 → 21 tables, 10 → 11 migrations); results are reported in the Prompt 15
implementation report.
