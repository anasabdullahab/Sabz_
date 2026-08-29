# Prompt 14: Farmer Community Foundation

## Purpose

A simple, agriculture-focused community where authenticated SABZ farmers can
**Ask → Share → Discuss → Learn**: post agricultural experiences, questions
and farm/crop updates, discuss them through comments, and optionally attach a
safe image reference to a post.

> **The SABZ Farmer Community is a user-generated discussion feature. Community
> posts and comments represent the views and experiences of individual users
> and are not guaranteed to be professional agricultural advice or verified
> facts.**

The feature is deliberately a **discussion foundation, not a social-media
platform**: no likes, reactions, followers, messaging, groups, feeds ranking
or AI moderation. Content is persistent user-generated data stored in the SABZ
database and protected by soft delete.

## User flow

1. A farmer logs in (`POST /api/auth/login`) and reads the newest-first
   community feed (`GET /api/community/posts`).
2. The farmer creates a post with text (and optionally a safe image URL).
3. Other farmers open the post (`GET /api/community/posts/{postId}`) and read
   its oldest-first comments, then reply
   (`POST /api/community/posts/{postId}/comments`).
4. A farmer can delete their own post or comment at any time; deleted content
   disappears from all normal queries (soft delete).

## Domain model

| Entity | Properties | Notes |
| --- | --- | --- |
| `CommunityPost` | `Id`, `UserId`, `Content` (≤ 2000), `ImageUrl` (nullable, ≤ 2048), `CreatedAt`, `UpdatedAt?`, `IsDeleted` | One row per farmer post; `UserId` always JWT-derived |
| `CommunityComment` | `Id`, `PostId`, `UserId`, `Content` (≤ 1000), `CreatedAt`, `UpdatedAt?`, `IsDeleted` | Belongs to exactly one post |

Relationships: a user creates many posts, a post has many comments, a user
creates many comments. Foreign keys: `CommunityPosts.UserId → Users (Restrict)`,
`CommunityComments.UserId → Users (Restrict)`, `CommunityComments.PostId →
CommunityPosts (Cascade)`. A single cascade path keeps SQL Server constraint
1785 satisfied; user deletion never silently destroys community content.

The project's existing audit convention is reused (each entity carries its own
`CreatedAt`/`UpdatedAt?`/flags). No second audit or soft-delete system was
introduced: soft delete is enforced by explicit `IsDeleted` filters in the
repository queries, consistent with the rest of the codebase.

## Endpoints

All seven endpoints require `[Authorize]` (authenticated reads are consistent
with the rest of SABZ). The controller is thin; business rules live in
`CommunityService`, persistence in `CommunityPostRepository` /
`CommunityCommentRepository`.

| Route | Behaviour |
| --- | --- |
| `GET /api/community/posts?page=1&pageSize=20` | DB-side paginated feed, newest first (`CreatedAt DESC, Id DESC`); `pageSize` max 50, invalid page/pageSize → `400` |
| `POST /api/community/posts` | Create a post from `{ content, imageUrl? }`; the JWT user becomes the author |
| `GET /api/community/posts/{postId}` | Post plus a bounded first page (≤ 50) of its oldest-first comments; soft-deleted → `404` |
| `DELETE /api/community/posts/{postId}` | Owner-only soft delete (`204`); also soft-deletes every visible comment of the post |
| `GET /api/community/posts/{postId}/comments?page=1&pageSize=20` | DB-side paginated comments, oldest first (`CreatedAt ASC, Id ASC`) |
| `POST /api/community/posts/{postId}/comments` | Create a comment from `{ content }` on an existing post |
| `DELETE /api/community/comments/{commentId}` | Owner-only soft delete (`204`) |

Feed/comment pages use the small reusable `PagedResult<T>` envelope:
`{ items, page, pageSize, totalCount, totalPages }`.

## Authorization and ownership

- Identity is always `ClaimTypes.NameIdentifier` from the JWT. No DTO accepts
  a client-supplied `UserId`; impersonation is impossible by design.
- Deleting another user's post or comment → `ForbiddenException` → `403`.
- Unknown post/comment (including soft-deleted) → `NotFoundException` → `404`.
- No token / malformed token → `401`. Validation failures → `400` via the
  existing `GlobalExceptionMiddleware`; the controller contains no try/catch.
- Responses expose only the author display name (`User.FullName`) and a
  server-computed `isOwnedByCurrentUser` flag. Never `UserId`, email, phone,
  password hash, token or key material.

## Validation rules

- Post content: required, non-whitespace after trim, ≤ **2000** characters.
- Comment content: required, non-whitespace after trim, ≤ **1000** characters.
- Comments must target an existing, non-deleted post.
- `page ≥ 1`, `1 ≤ pageSize ≤ 50`; anything else → `400`.

## Image handling

Image attachment is optional and intentionally minimal:

- Only a **safe URL reference** is stored (`ImageUrl`, ≤ 2048 chars) - never
  binary data and never a filesystem path.
- Accepted: absolute `http://` or `https://` URLs.
- Rejected (`400`): `C:\...` / `E:\...` Windows paths, `/etc/...` Unix paths,
  `file://` URLs, relative paths and non-URL strings.
- No cloud storage or upload pipeline is invented; actual image storage stays
  out of scope until a real mechanism exists in the project.

## Database changes

One additive migration, `AddCommunityFoundation`:

- New tables: `CommunityPosts`, `CommunityComments`
  (table count 16 → 18, migrations 9 → 10).
- Indexes: `IX_CommunityPosts_UserId_CreatedAt`, `IX_CommunityPosts_CreatedAt`,
  `IX_CommunityComments_PostId_CreatedAt`, `IX_CommunityComments_UserId_CreatedAt`.
- `CreatedAt` defaults to `GETUTCDATE()`; `Content` `nvarchar(2000)` /
  `nvarchar(1000)`; `ImageUrl` `nvarchar(2048)` nullable.
- No Prompt 1–13 table was altered. `dotnet ef migrations
  has-pending-model-changes` reports no pending changes after applying.

## Performance

- All reads are `AsNoTracking()` SQL projections into read models - no entities
  on read paths, no N+1: author display names and visible comment counts are
  computed in SQL in one round-trip per page.
- Pagination is DB-side (`OFFSET/FETCH`); the community is never loaded into
  memory.
- Ordering is deterministic: posts `CreatedAt DESC, Id DESC`, comments
  `CreatedAt ASC, Id ASC`.
- Soft-deleted rows are excluded from every normal query.

## Limitations

- No post/comment editing (create/read/delete only), by design.
- Comment threads are flat (no replies-to-comments).
- The post detail view returns only the first comment page (≤ 50); full
  threads come from the paginated comments endpoint.
- Images are references only; SABZ does not host or validate that the URL
  actually resolves.
- Community content is user-generated and unverified (see the statement at the
  top of this document).

## Deliberate exclusions

Equipment sharing, marketplace/buying/selling, payments, private messaging or
chat, followers/friends, likes/reactions/shares/stories/livestreaming, AI
moderation or AI-generated content, community notifications (Prompt 8 is not
touched), background jobs, recommendation/popularity algorithms, advertising,
and anything financial (loans, credit, banking, insurance, investments).
No React frontend was built; the API is Swagger-documented for future clients.

## Testing

`test-community.ps1` (intentionally untracked, idempotent) covers all 35 spec
checks with fixture users `test21@example.com` / `userb3@example.com` and a
`CM ` content prefix cleaned through the public API before and after each run:

- Authentication (1–5): every endpoint rejects missing/malformed tokens.
- Posts (6–16): create/read/feed/pagination/ordering plus rejection of
  whitespace, empty and overlong content and of unsafe image references.
- Ownership (17–19): owner-only delete, foreign delete `403`, unknown `404`.
- Comments (20–28): create/read/pagination, correct-post association, content
  validation, owner-only delete, unknown comment `404`.
- Security (29–31): no `UserId`/password/token/key/PII leakage; cross-user
  isolation verified.
- Persistence (32–35): posts/comments survive request cycles; soft-deleted
  posts and comments disappear from every normal query.

Prompt 1–13 regression suites were re-run afterwards; results are reported in
the Prompt 14 implementation report.
