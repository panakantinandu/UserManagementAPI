# Middleware Pipeline

To comply with TechHive Solutions' corporate policy, the API now runs three
custom middleware components on every request.

## Components

### 1. `ExceptionHandlingMiddleware` (error handling)

Wraps the entire pipeline in a `try`/`catch`. Any unhandled exception is
logged (via `ILogger`) and converted into a consistent JSON response instead
of crashing the app or leaking a stack trace:

```json
{ "error": "Internal server error." }
```

Status code: `500`.

### 2. `TokenAuthenticationMiddleware` (authentication)

Requires an `Authorization: Bearer <token>` header on every request except
`/swagger/*` (so the API docs stay browsable). The expected token is read
from configuration (`Authentication:ApiToken` — `appsettings.json`,
environment variables, or user secrets).

- Missing/invalid token → `401 Unauthorized`, `{ "error": "Missing or invalid API token." }`
- Server has no token configured → `401 Unauthorized`, `{ "error": "API authentication is not configured." }` (fails closed rather than allowing all traffic)
- Token comparison uses `CryptographicOperations.FixedTimeEquals` to avoid timing attacks.

For local development, `appsettings.Development.json` sets
`Authentication:ApiToken` to `dev-secret-token`. **Do not use this value
outside local development** — set a real secret via an environment variable
(`Authentication__ApiToken`) or user secrets before deploying.

### 3. `RequestResponseLoggingMiddleware` (logging)

Logs the HTTP method and path when a request arrives, and the method, path,
and resulting status code once the response is ready — for every request
that reaches this stage.

## Pipeline order

```
ExceptionHandlingMiddleware
  -> TokenAuthenticationMiddleware
    -> RequestResponseLoggingMiddleware
      -> Swagger (dev only) / HTTPS redirection / routing / controllers
```

This is the order corporate policy specifies: error handling outermost so it
can catch failures anywhere below it, authentication next so unauthorized
requests are rejected before doing any real work, logging last so it only
runs for requests that were actually authorized.

**Trade-off:** because logging sits after authentication, a rejected (401)
request is not recorded by `RequestResponseLoggingMiddleware`. It is not
silently dropped, though — `TokenAuthenticationMiddleware` logs a warning
for every rejected request itself, so the audit trail is split across two
loggers rather than lost: authorized traffic is logged by the logging
middleware, rejected traffic is logged by the auth middleware.

## Testing performed

> Same caveat as [DEBUGGING.md](DEBUGGING.md): no .NET SDK is available in
> this environment, so these were traced through the code by hand, not
> executed. Verify with `dotnet run` and the commands below.

| Scenario | Request | Expected result |
|----------|---------|------------------|
| No token | `GET /api/users` (no `Authorization` header) | `401`, `{ "error": "Missing or invalid API token." }` |
| Wrong token | `GET /api/users` with `Authorization: Bearer wrong` | `401`, same body |
| Valid token | `GET /api/users` with `Authorization: Bearer dev-secret-token` | `200` with users |
| Swagger without token | `GET /swagger/index.html` (no header) | `200` (exempt) |
| Unhandled exception | Any request that triggers an unexpected server error | `500`, `{ "error": "Internal server error." }` |
| Logging | Any authorized request | Console shows a `Request: <METHOD> <PATH>` line followed by a `Response: <METHOD> <PATH> responded <STATUS>` line |

```bash
dotnet run &

# Rejected: no token
curl -i http://localhost:5223/api/users

# Rejected: wrong token
curl -i http://localhost:5223/api/users -H "Authorization: Bearer wrong-token"

# Accepted
curl -i http://localhost:5223/api/users -H "Authorization: Bearer dev-secret-token"

# Swagger stays open
curl -i http://localhost:5223/swagger/index.html
```

## How Copilot's suggestions helped

- Suggested using `CryptographicOperations.FixedTimeEquals` for the token
  comparison instead of `==`/`string.Equals`, avoiding a timing side-channel.
- Flagged that calling `app.UseAuthorization()` (the built-in ASP.NET Core
  authorization middleware) does nothing here since no `[Authorize]`
  attributes or policies are configured — the real access control is our
  custom `TokenAuthenticationMiddleware`, so `UseAuthorization()` was left
  in only because the framework's routing/MVC integration expects it to be
  present, not because it enforces anything on its own.
- Recommended exempting `/swagger` from the token check so the API remains
  self-documenting without requiring a token just to view the docs.
- Pointed out the logging-after-auth ordering means rejected requests
  wouldn't appear in the request/response log, and suggested logging
  rejections directly inside the auth middleware to keep the audit trail
  complete without breaking the required middleware order.
