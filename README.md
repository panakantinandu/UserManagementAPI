# User Management API

A simple ASP.NET Core Web API built for TechHive Solutions' internal HR and IT
teams to create, retrieve, update, and delete user records.

## Features

- **GET** `/api/users` — retrieve all users (supports `?page=&pageSize=` pagination)
- **GET** `/api/users/{id}` — retrieve a single user by ID
- **POST** `/api/users` — create a new user
- **PUT** `/api/users/{id}` — update an existing user
- **DELETE** `/api/users/{id}` — remove a user by ID
- Input validation (required fields, email format, duplicate-email detection)
- Global exception-handling middleware so unhandled errors return a safe
  `500` response instead of crashing the API
- Token-based authentication — every `/api/*` request requires an
  `Authorization: Bearer <token>` header
- Request/response logging for every authorized request
- Swagger / OpenAPI UI for exploring and testing the API

See [DEBUGGING.md](DEBUGGING.md) for the bugs found in the initial release
and how they were fixed, and [MIDDLEWARE.md](MIDDLEWARE.md) for details on
the logging, error-handling, and authentication middleware.

## Tech stack

- ASP.NET Core Web API (.NET 6)
- In-memory data store (no external database required)
- Swashbuckle (Swagger)

## Prerequisites

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or later

## Getting started

```bash
# Restore dependencies
dotnet restore

# Run the API
dotnet run
```

By default the API starts at `https://localhost:7123` and `http://localhost:5223`
(see `Properties/launchSettings.json`). Once running, browse to `/swagger` for
an interactive API explorer.

Every `/api/*` request must include a bearer token:

```bash
curl -i http://localhost:5223/api/users -H "Authorization: Bearer dev-secret-token"
```

The development token (`dev-secret-token`) is set in
`appsettings.Development.json`. For any other environment, set a real value
via the `Authentication__ApiToken` environment variable or user secrets —
see [MIDDLEWARE.md](MIDDLEWARE.md).

## API reference

| Method | Route              | Description                    | Success | Failure |
|--------|---------------------|--------------------------------|---------|---------|
| GET    | `/api/users`         | List all users (`?page=&pageSize=` optional) | 200 | — |
| GET    | `/api/users/{id}`    | Get a single user               | 200     | 400, 404 |
| POST   | `/api/users`         | Create a new user                | 201     | 400, 409 |
| PUT    | `/api/users/{id}`    | Update an existing user          | 204     | 400, 404, 409 |
| DELETE | `/api/users/{id}`    | Delete a user                     | 204     | 400, 404 |

- `400` — invalid input (missing/invalid fields, non-positive ID, missing body)
- `404` — no user exists with the given ID
- `409` — another user already has the given email

Error responses have the shape `{ "error": "message" }` (or ASP.NET Core's
standard validation problem details for `400`s from model validation).

### User model

```json
{
  "id": 1,
  "firstName": "Jane",
  "lastName": "Doe",
  "email": "jane.doe@example.com"
}
```

`firstName`, `lastName`, and `email` are required; `email` must be a valid
email address.

## Project structure

```
Controllers/    API controllers (UsersController)
Models/         Data models (User)
Services/       Data access (IUserRepository, InMemoryUserRepository)
Middleware/     Exception handling, token auth, request/response logging
Program.cs      App startup and middleware configuration
```

## Notes

- User data is stored in memory and resets whenever the API restarts.
- Swagger UI is enabled only in the Development environment.
