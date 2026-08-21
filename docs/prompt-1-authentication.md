# PROMPT 1 — Authentication Foundation

## Purpose

User accounts and JWT authentication for the SABZ backend. Every other
feature builds on this: the authenticated user id (from the JWT) is the only
identity the server trusts.

## Endpoints (`api/auth`)

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | No | Create an account (FullName, Email, Password, ConfirmPassword, optional PhoneNumber, PreferredLanguage) |
| POST | `/api/auth/login` | No | Login with Identifier (email) + Password, returns JWT |
| GET | `/api/auth/me` | Yes | Current user profile |

## Implementation highlights

- `User` entity: GUID id, unique email, BCrypt password hash, role, timestamps.
- Passwords hashed with BCrypt (`IPasswordService`); plaintext never stored.
- Tokens issued by `ITokenService`: issuer/audience/key from the `Jwt`
  configuration section; the user GUID is stored in `ClaimTypes.NameIdentifier`.
- `AuthService` handles validation (duplicate email, wrong credentials) by
  throwing domain exceptions mapped to 400/401 by the exception middleware.
- Controllers are thin: they read the user id from the JWT claims and delegate
  to application services. Clients cannot impersonate others because user ids
  in request payloads are ignored for ownership decisions.

## Security rules

- Every non-auth endpoint is `[Authorize]`d; missing/invalid token -> 401.
- Cross-user access to owned resources -> 403 (`ForbiddenException`).
- The JWT is short-lived; there is no refresh-token mechanism in this foundation.
