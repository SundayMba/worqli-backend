# Servika Backend

ASP.NET Core (.NET 9) modular monolith for the Servika service marketplace.
Clean Architecture layering; PostgreSQL + Redis via Docker for local dev.

> **Status:** Slice 0 — scaffold only. Health endpoint + Swagger + Docker.
> No business features yet (auth, bookings, payments come in later slices).

## Solution layout

```text
worqli-backend/
  Servika.sln
  docker-compose.yml          # PostgreSQL + Redis for local dev
  src/
    Servika.Api/              # Controllers/endpoints, Swagger, SignalR hubs, middleware
    Servika.Application/      # Use cases, DTOs, validation, orchestration
    Servika.Domain/           # Entities, enums, domain rules, booking state machine
    Servika.Infrastructure/   # EF Core, repositories, payment/storage/notification adapters
    Servika.Contracts/        # Shared request/response contracts
    Servika.Worker/           # Background jobs (reconciliation, stale-session cleanup)
  tests/
    Servika.UnitTests/
    Servika.IntegrationTests/
```

**Reference direction:** Api → Application + Infrastructure + Contracts ·
Infrastructure → Application + Domain · Application → Domain + Contracts ·
Domain depends on nothing.

## Prerequisites

- .NET SDK 9.x (`dotnet --version`)
- Docker + Docker Compose

## Run locally

```bash
# 1. Start Postgres + Redis
docker compose up -d

# 2. Run the API (from repo root)
dotnet run --project src/Servika.Api --launch-profile http
```

The API listens on **http://localhost:5046** (bound to 0.0.0.0 so physical
devices on your LAN can reach it).

### Verify

| URL | Expect |
|-----|--------|
| http://localhost:5046/health | `Healthy` |
| http://localhost:5046/ | `{"service":"Servika API","status":"ok"}` |
| http://localhost:5046/api/v1/ping | `{"message":"pong"}` |
| http://localhost:5046/swagger | Swagger UI |

## Configuration

`src/Servika.Api/appsettings.json` holds templates for `ConnectionStrings`
(Postgres, Redis) and `Jwt`. **Replace the JWT signing key and DB password
with real secrets** (via user-secrets or env vars) before any shared/prod use —
do not commit real secrets.

## Build & test

```bash
dotnet build Servika.sln
dotnet test Servika.sln
```

## Next slice

Slice 1 — Authentication & RBAC (register / login / me / refresh, JWT, roles).
See `worqli-mobile/screen-context/07-batch-7-backend-api-high-fidelity/prompts/24-auth-rbac.md`.
