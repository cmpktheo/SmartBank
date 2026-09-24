# SmartBank — demo web banking on microservices

A demo **web banking app**: register and log in (with MFA), open accounts, transfer money between accounts, browse transactions and statements, manage debit cards (freeze, limits, CVV reveal) — through an Angular SPA backed by .NET microservices.

## Functionality

- **Auth + MFA** — email/password login, OTP challenge, refresh tokens, logout with token blacklist.
- **Accounts** — open accounts (dummy GB IBANs), look up by IBAN, view your portfolio.
- **Transfers + ledger** — idempotent transfers with double-entry journal, recent transactions, full statements (incl. CSV export).
- **Cards** — list, freeze/unfreeze, daily limits, rate-limited CVV reveal, encrypted PAN storage.
- **Notifications** — async transfer notifications via event consumer.
- **Observability** — correlated logs, metrics, traces and dashboards out of the box.

## Tech stack

| Layer | Choice |
|---|---|
| Backend | .NET 10, ASP.NET Core minimal APIs, EF Core 10 |
| Gateway | YARP reverse proxy + JWT validation |
| Service-to-service | gRPC (Ledger → Customer) + RabbitMQ events |
| Data / cache | Postgres 16 (one DB per service) + Redis 7 |
| Auth | OpenIddict + JWT (HS256) |
| Frontend | Angular 20, Tailwind, Vitest + Playwright |
| Observability | OpenTelemetry → Collector → Prometheus / Loki / Tempo → Grafana |

## Microservices architecture

- **Gateway** `:5100` — single public entry point, routes `/api/**` to the right service.
- **Identity** `:5101` — users, passwords, MFA, tokens (owns `smartbank_identity`).
- **Customer** `:5102` + gRPC `:6102` — customers, accounts, balance holds (owns `smartbank_customer`).
- **Ledger** `:5103` — transfers, journal, statements, idempotency + outbox (owns `smartbank_ledger`).
- **Cards** `:5104` — card lifecycle, limits, PAN protection (owns `smartbank_cards`).
- **Notification** `:5105` — async-only event consumer (owns `smartbank_notification`).

Sync reads/reservations go over gRPC; facts other services react to (e.g. money moved) go over RabbitMQ (`smartbank.ledger` exchange). Each service owns its database — no shared DB.

## Run it

```powershell
Copy-Item ".env.example" ".env"   # first time only
docker compose up -d
./scripts/migrate-seed.ps1        # migrate + seed (run `docker compose down -v` first to re-seed)
./scripts/start-apis.ps1          # APIs 5101-5105 + gateway 5100
cd frontend/smartbank-web; npm ci; npm start   # SPA on :4200
```

Seed logins (password `123456`): `alex.morgan@smartbank.test`, `jordan.lee@smartbank.test`.

## Docs

Full developer docs (static site, no build step) live in [`docs/`](docs/) — open `docs/index.html`:

- **Tech stack** — pinned versions and docker images
- **Architecture** — container diagrams, DB-per-service, gateway routes
- **Communication** — REST vs gRPC vs events, with sequence diagrams
- **Services / Infra / Building blocks / Contracts / Runbook** — one page per topic
