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

**`.env.example` is the full, grouped checklist** of every setting — what's
required in Production, what's an optional integration (safe dev fallback when
blank), and the feature flags. Copy it to `.env` on the server and fill in real
values; never commit real secrets.

Key rules:
- **Local dev needs no secrets.** Leave everything blank and the API uses dev
  stubs (logs OTP codes, stub payments/payouts/SMS, phone gate off). Prefer
  `dotnet user-secrets` for any keys you do set locally, never `appsettings.json`.
- **Production fail-closed guard.** The API refuses to boot unless
  `Jwt__SigningKey` is a real ≥32-char secret, `Paystack__SecretKey` is set (a
  blank key would select the fail-open stub), and `Cors:AllowedOrigins` is
  populated (it is, in `appsettings.json`). Swagger is off in Production unless
  `Swagger__Enabled=true`.
- **One key, two directions.** `Paystack__SecretKey` drives charges *and* payouts
  (Transfers) *and* the bank list. Dashboard: turn off "Transfers OTP" + get
  approved for Transfers with a funded balance.
- **Phone verification is optional/dormant.** Set `Sms__ApiKey` + `Sms__SenderId`
  (Termii) then `Auth__RequirePhoneForBooking=true` to enable it (the guard blocks
  boot if the gate is on while `Sms__ApiKey` is blank). Provision the Termii
  WhatsApp OTP template + transactional sender id first — that has lead time.

`docker-compose.prod.yml` maps the friendly `.env` names (e.g.
`PAYSTACK_SECRET_KEY`) to the ASP.NET config keys (`Paystack__SecretKey`).
`src/Servika.Api/appsettings.json` holds non-secret defaults + the `Cors` origins.

## Build & test

```bash
dotnet build Servika.sln
dotnet test Servika.sln
```

## Next slice

Slice 1 — Authentication & RBAC (register / login / me / refresh, JWT, roles).
See `worqli-mobile/screen-context/07-batch-7-backend-api-high-fidelity/prompts/24-auth-rbac.md`.
