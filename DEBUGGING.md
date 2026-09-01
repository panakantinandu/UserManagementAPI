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

> **Note:** No .NET SDK is installed in the environment these fixes were
> written in (only the .NET 6 runtime), so the API could not actually be
> built or run here. The table below was verified by tracing each request
> through the updated code path by hand, not by executing it. Run the
> commands yourself with `dotnet run` and the `curl` examples below (or
> Swagger UI at `/swagger`) to confirm before relying on this as a test
> record.

| Scenario | Request | Expected result |
|----------|---------|------------------|
| List users | `GET /api/users` | `200` with seeded users |
| Paginated list | `GET /api/users?page=1&pageSize=1` | `200` with 1 user |
| Get existing user | `GET /api/users/1` | `200` with user |
| Get non-existent user | `GET /api/users/999` | `404` with error message |
| Get invalid id | `GET /api/users/0` | `400` with error message |
| Create valid user | `POST /api/users` with valid body | `201` + `Location` header |
| Create with missing fields | `POST /api/users` with `{}` | `400` with field errors |
| Create with invalid email | `POST /api/users` with `"email": "not-an-email"` | `400` with field errors |
| Create with duplicate email | `POST /api/users` reusing an existing email | `409` with error message |
| Create with empty body | `POST /api/users` with no body | `400` with error message |
| Update existing user | `PUT /api/users/1` with valid body | `204` |
| Update non-existent user | `PUT /api/users/999` with valid body | `404` with error message |
| Update to a duplicate email | `PUT /api/users/1` using user 2's email | `409` with error message |
| Delete existing user | `DELETE /api/users/1` | `204` |
| Delete non-existent user | `DELETE /api/users/999` | `404` with error message |

### Commands to verify

```bash
dotnet run &

curl -i http://localhost:5223/api/users
curl -i http://localhost:5223/api/users/999
curl -i http://localhost:5223/api/users/0
curl -i -X POST http://localhost:5223/api/users -H "Content-Type: application/json" -d "{}"
curl -i -X POST http://localhost:5223/api/users -H "Content-Type: application/json" -d "{\"firstName\":\"Jane\",\"lastName\":\"Doe\",\"email\":\"jane.doe@example.com\"}"
curl -i -X DELETE http://localhost:5223/api/users/999
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
