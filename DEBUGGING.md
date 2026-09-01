# Debugging & Optimization Summary

This document covers the debugging pass performed after TechHive Solutions
reported issues with the initial release of the User Management API:
users being created without proper validation, errors when retrieving
non-existent users, and occasional crashes from unhandled exceptions.

GitHub Copilot's inline suggestions and chat were used throughout to spot
likely failure points in `UsersController` and the in-memory repository,
and to propose idiomatic ASP.NET Core fixes (middleware-based exception
handling, `ModelState` validation, `ConcurrentDictionary`-safe lookups).

## Bugs identified and fixed

| # | Bug | Root cause | Fix |
|---|-----|------------|-----|
| 1 | Users could be created with duplicate emails | No uniqueness check anywhere in the create/update path | Added `IUserRepository.EmailExists`; `POST`/`PUT` now return `409 Conflict` on a duplicate email |
| 2 | Inconsistent data from leading/trailing whitespace (e.g. `"John "` vs `"John"`) | Input was stored exactly as received | Names and emails are trimmed before being persisted (`InMemoryUserRepository.Normalize`) |
| 3 | `GET/PUT/DELETE /api/users/{id}` accepted non-positive IDs (e.g. `0`, `-1`) and just fell through to a generic 404 | No explicit ID validation | Added an explicit `id <= 0` check returning `400 Bad Request` with a clear message, distinct from "not found" |
| 4 | `POST`/`PUT` with a missing/empty request body could reach the action with a `null` model | No null guard before validation | Added explicit `null` checks returning `400 Bad Request` before `ModelState` is evaluated |
| 5 | Retrieving a non-existent user returned a bare `404` with no explanation | `NotFound()` returned no body | Responses now include a JSON error message (e.g. `No user found with id 5.`) |
| 6 | Any unhandled exception (bad input the framework doesn't catch, future repository/DB errors, etc.) would surface as a raw ASP.NET Core error page or crash the request pipeline | No global exception handling middleware | Added `ExceptionHandlingMiddleware`, registered first in the pipeline, which logs the exception and returns a safe structured `500` JSON response instead of leaking internals or taking down the app |
| 7 | No visibility into what the API was doing, making failures hard to diagnose in production | No logging in the controller | Injected `ILogger<UsersController>` and added log entries for not-found, conflict, and successful create/update/delete operations |
| 8 | `GET /api/users` always returned the entire user set, re-sorted on every call, with no way to limit response size | No pagination | Added optional `page` and `pageSize` query parameters (`GET /api/users?page=1&pageSize=20`); omitting them preserves the original full-list behavior |

Validation that was already correct and didn't need changes: `[Required]`,
`[MaxLength(50)]`, and `[EmailAddress]` on the `User` model, combined with
`[ApiController]`'s automatic `400` response on invalid `ModelState`,
already rejected missing names, over-length names, and malformed emails.

## Testing performed

Built and run for real with `dotnet build` / `dotnet run` (.NET 6 SDK) and
exercised with `curl` against a live local instance. All requests below
were sent with `Authorization: Bearer dev-secret-token`, required by the
token-authentication middleware added afterward (see
[MIDDLEWARE.md](MIDDLEWARE.md)).

| Scenario | Request | Expected result | Actual result |
|----------|---------|------------------|----------------|
| List users | `GET /api/users` | `200` with seeded users | `200`, both seeded users returned |
| Paginated list | `GET /api/users?page=1&pageSize=1` | `200` with 1 user | `200`, exactly 1 user returned |
| Get existing user | `GET /api/users/1` | `200` with user | `200` with the user |
| Get non-existent user | `GET /api/users/999` | `404` with error message | `404`, `{"error":"No user found with id 999."}` |
| Get invalid id | `GET /api/users/0` | `400` with error message | `400`, `{"error":"Id must be a positive integer."}` |
| Create valid user | `POST /api/users` with valid body | `201` + `Location` header | `201`, `Location: /api/Users/3`, user returned with assigned `id` |
| Create with missing fields | `POST /api/users` with `{}` | `400` with field errors | `400` `ValidationProblemDetails` listing `FirstName`, `LastName`, `Email` as required |
| Create with invalid email | `POST /api/users` with `"email": "not-an-email"` | `400` with field errors | `400`, `Email` field flagged "not a valid e-mail address" |
| Create with duplicate email | `POST /api/users` reusing an existing email | `409` with error message | `409`, `{"error":"A user with email '...' already exists."}` |
| Create with zero-length body | `POST /api/users` with no body at all | `400` | `400` — the framework's own model binder rejects it before our action runs (`"A non-empty request body is required."`), so our own `user is null` guard never actually fires for *this* case (see note below) |
| Update existing user | `PUT /api/users/1` with valid body | `204` | `204`, and a follow-up `GET` confirmed the change persisted |
| Update non-existent user | `PUT /api/users/999` with valid body | `404` with error message | `404`, `{"error":"No user found with id 999."}` |
| Update to a duplicate email | `PUT /api/users/1` using user 2's email | `409` with error message | `409`, `{"error":"A user with email '...' already exists."}` |
| Update with invalid id | `PUT /api/users/-1` with valid body | `400` | `400`, `{"error":"Id must be a positive integer."}` |
| Delete existing user | `DELETE /api/users/1` | `204` | `204` |
| Delete non-existent user | `DELETE /api/users/999` | `404` with error message | `404`, `{"error":"No user found with id 999."}` |
| Malformed JSON body | `POST /api/users` with `{not valid json` | `400` | `400` — caught by the framework's JSON parser before reaching our code, not by `ExceptionHandlingMiddleware` |

**Correction from the original draft of this document:** the `user is null`
guard in `CreateUser`/`UpdateUser` turned out to be effectively unreachable
under ASP.NET Core 6's default `[ApiController]` behavior — a zero-length
or literal `null` JSON body is rejected by the framework's own model
binding *before* the action method runs, always producing its standard
`ValidationProblemDetails` response rather than ours. The guard is
harmless (dead code, not wrong code) and left in place as defensive
documentation of intent, but it doesn't add behavior beyond what the
framework already enforces.

### Commands used

```bash
dotnet build
dotnet run &

TOKEN="dev-secret-token"
curl -i http://localhost:5223/api/users -H "Authorization: Bearer $TOKEN"
curl -i http://localhost:5223/api/users/999 -H "Authorization: Bearer $TOKEN"
curl -i http://localhost:5223/api/users/0 -H "Authorization: Bearer $TOKEN"
curl -i -X POST http://localhost:5223/api/users -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{}"
curl -i -X POST http://localhost:5223/api/users -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"firstName\":\"Jane\",\"lastName\":\"Doe\",\"email\":\"jane.doe@example.com\"}"
curl -i -X DELETE http://localhost:5223/api/users/999 -H "Authorization: Bearer $TOKEN"
```

## How Copilot's suggestions helped

- Flagged that `[ApiController]`'s automatic model validation already
  handles most "empty/missing field" cases, so effort could focus on gaps
  it *doesn't* cover: duplicate emails, non-positive IDs, and null bodies.
- Suggested the middleware-based `try`/`catch` pattern (`ExceptionHandlingMiddleware`)
  instead of wrapping every controller action individually, avoiding
  repeated `try`/`catch` blocks and guaranteeing every request path — including
  future endpoints — is protected.
- Recommended `ConcurrentDictionary`-safe patterns (`TryGetValue`,
  `TryRemove`) were already in place, confirming the in-memory store isn't
  a source of race conditions.
- Pointed out that returning bare `NotFound()`/`BadRequest()` without a
  message makes API consumers guess; suggested adding structured JSON
  error bodies consistently across the controller.
- Live testing surfaced one thing static review missed: the manual
  `user is null` guard doesn't actually get exercised the way it looks
  like it should, because `[ApiController]` intercepts empty/`null`
  bodies first. Worth knowing before assuming a guard like that is doing
  what it appears to.
