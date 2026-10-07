# SocialPlatform – Backend

A microservices backend for a social platform, built with **ASP.NET Core 8**, **PostgreSQL**, and an **Ocelot** API gateway. Clients talk only to the gateway, which routes each request to the right service.

## Architecture

```
                    ┌──────────────────────────┐
  Client ──────────▶│  API Gateway (Ocelot)    │  :5000
                    │  JWT · rate limit · logs │
                    └────────────┬─────────────┘
        ┌──────────────┬─────────┴────┬──────────────────┐
        ▼              ▼              ▼                  ▼
  User Service    Post Service   Notification Svc   Comment Service
     :5001           :5002           :5003              :5004
        │              │              │
        ▼              ▼              ▼
   postgres-user  postgres-post  postgres-notification
     (users)        (posts)       (notifications)
```

Each service owns its own database.

| Component | Path | Port | Status |
|---|---|---|---|
| API Gateway | `gateway/ApiGateway` | 5000 | Routing, JWT auth, rate limiting, request logging, `/health` |
| User Service | `services/user-service/UserService` | 5001 | Registration, login (JWT), user lookup |
| Post Service | `services/post-service/PostService` | 5002 | Create post, get post, paged feed |
| Notification Service | `services/notification-service/NotificationService` | 5003 | Scaffold only (`/health`) |
| Comment Service | `services/comment-service/CommentService` | 5004 | Scaffold only (`/health`) |
| Shared library | `shared/Shared` | – | Shared code referenced by all services |

## Tech stack

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core 8 + Npgsql (PostgreSQL 15)
- Ocelot API gateway
- JWT bearer authentication; BCrypt password hashing
- Serilog (console + daily rolling files under each project's `logs/`)
- Swagger / Swashbuckle (Development environment only)

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for PostgreSQL)
- Optional: `dotnet-ef` for creating migrations — `dotnet tool install --global dotnet-ef`

## Getting started

### 1. Start the databases

```bash
docker compose up -d postgres-user postgres-post postgres-notification
```

| Container | Host port | Database |
|---|---|---|
| postgres-user | 5432 | `users` |
| postgres-post | 5433 | `posts` |
| postgres-notification | 5434 | `notifications` |

Default credentials are `postgres` / `postgres`.

### 2. Configure settings

`appsettings.*.json` and `.env` are git-ignored, so create them locally. Each service that uses a database or JWT needs:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=users;Username=postgres;Password=postgres"
  },
  "JwtSettings": {
    "Secret": "<at least 32 random characters, identical in every service>",
    "Issuer": "social-platform",
    "Audience": "social-platform-api",
    "ExpiryMinutes": 1440
  }
}
```

- Use port **5433** and database `posts` for the Post Service, and port **5434** and database `notifications` for the Notification Service.
- The gateway needs the same `JwtSettings`.
- `JwtSettings.Secret` must be the same in the User Service (which signs tokens) and in the gateway and Post Service (which validate them).

You can also keep the secret out of files with user secrets:

```bash
dotnet user-secrets set "JwtSettings:Secret" "<your-secret>" --project services/user-service/UserService
```

### 3. Run the services

Open `SocialPlatform.sln` in Visual Studio and set multiple startup projects, or run each one in its own terminal:

```bash
dotnet run --project services/user-service/UserService
dotnet run --project services/post-service/PostService
dotnet run --project services/notification-service/NotificationService
dotnet run --project services/comment-service/CommentService
dotnet run --project gateway/ApiGateway
```

The User and Post services apply EF Core migrations automatically on startup.

Swagger UI is available on each service at `http://localhost:<port>/swagger` in Development.

## API

All requests go through the gateway at `http://localhost:5000`.

### Users — `/api/users/*` → User Service `/api/*`

| Method | Gateway path | Auth | Description |
|---|---|---|---|
| POST | `/api/users/auth/register` | – | Register (`username`, `email`, `password`) |
| POST | `/api/users/auth/login` | – | Log in (`email`, `password`); returns `id`, `username`, `token` |
| GET | `/api/users/auth/{id}` | – | Get a user by ID |
| GET | `/api/users/auth/health` | – | Health check |

### Posts — `/api/posts/*` → Post Service

| Method | Gateway path | Auth | Description |
|---|---|---|---|
| POST | `/api/posts` | Bearer | Create a post (`content`) |
| GET | `/api/posts/{id}` | – | Get a post by ID |
| GET | `/api/posts/feed?pageNumber=1&pageSize=10` | – | Paged feed |
| GET | `/api/posts/health` | – | Health check |

### Notifications and comments

`/api/notifications/*` and `/api/comments/*` are routed to their services, which don't expose endpoints yet.

### Example

```bash
# Register
curl -X POST http://localhost:5000/api/users/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","email":"alice@example.com","password":"P@ssw0rd!"}'

# Log in and copy the token
curl -X POST http://localhost:5000/api/users/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","password":"P@ssw0rd!"}'

# Create a post
curl -X POST http://localhost:5000/api/posts \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"content":"Hello, world!"}'
```

## Rate limits

Set per route in `gateway/ApiGateway/ocelot.json`. Requests over the limit get HTTP `429`.

| Route | Limit |
|---|---|
| Users | 100 / min |
| Posts | 150 / min |
| Comments | 100 / min |
| Notifications | none |

## Database migrations

```bash
dotnet ef migrations add <Name> --project services/user-service/UserService
dotnet ef migrations add <Name> --project services/post-service/PostService
```

## Project structure

```
backend/
├── SocialPlatform.sln
├── docker-compose.yml
├── gateway/
│   └── ApiGateway/          # Ocelot config (ocelot.json), middleware
├── services/
│   ├── user-service/        # Controllers, Data, Models, Services, Migrations
│   ├── post-service/
│   ├── notification-service/
│   └── comment-service/
└── shared/
    └── Shared/              # Shared class library
```

## Known gaps

- `docker-compose.yml` defines containers for the gateway and services, but there are no Dockerfiles yet, and the build contexts (`./backend/...`) assume compose runs from the repository root. For now, use compose only for the databases.
- `ocelot.json` points downstream services at `localhost`. Running the services in Docker would require changing these to container names.
- The Notification and Comment services are scaffolds with no endpoints yet.
