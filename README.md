# User Management API

A simple ASP.NET Core Web API built for TechHive Solutions' internal HR and IT
teams to create, retrieve, update, and delete user records.

## Features

- **GET** `/api/users` — retrieve all users
- **GET** `/api/users/{id}` — retrieve a single user by ID
- **POST** `/api/users` — create a new user
- **PUT** `/api/users/{id}` — update an existing user
- **DELETE** `/api/users/{id}` — remove a user by ID
- Swagger / OpenAPI UI for exploring and testing the API

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

## API reference

| Method | Route              | Description                    | Success | Failure |
|--------|---------------------|--------------------------------|---------|---------|
| GET    | `/api/users`         | List all users                 | 200     | —       |
| GET    | `/api/users/{id}`    | Get a single user               | 200     | 404     |
| POST   | `/api/users`         | Create a new user                | 201     | 400     |
| PUT    | `/api/users/{id}`    | Update an existing user          | 204     | 400, 404|
| DELETE | `/api/users/{id}`    | Delete a user                     | 204     | 404     |

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
Program.cs      App startup and middleware configuration
```

## Notes

- User data is stored in memory and resets whenever the API restarts.
- Swagger UI is enabled only in the Development environment.
