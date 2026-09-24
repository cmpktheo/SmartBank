# SmartBank Runbook

## 1. Prerequisites

- Windows 10/11 with WSL2 enabled
- Docker Desktop installed (Linux containers mode)
- PowerShell, working directory `D:\Code\SmartBank`

> All images in `docker-compose.yml` (`postgres:16.6-alpine`, `redis:7.4-alpine`, `rabbitmq:3.13-management-alpine`, `otel-collector`, `prometheus`, `loki`, `tempo`, `grafana`) are Linux-only.
> On Windows you stay in PowerShell — Docker Desktop runs the Linux engine via WSL2 internally. Do not switch Docker Desktop to Windows containers.

## 2. Start infrastructure

```powershell
# optional, first time only (repo has no .env yet)
Copy-Item ".env.example" ".env"

# start all infra in background
docker compose up -d
docker compose ps
```

## 3. Verify engine

If `docker info` fails with `open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified`, Docker Desktop is not running:

```powershell
Start-Service com.docker.service -ErrorAction SilentlyContinue
Start-Process "$Env:ProgramFiles\Docker\Docker\Docker Desktop.exe"
docker info --format '{{.OSType}} / {{.Architecture}} / {{.OperatingSystem}}'
# expected: linux / x86_64 / Docker Desktop
```

Check tray icon: if you see `Switch to Windows containers`, you are already correctly on Linux containers. If you see `Switch to Linux containers`, click it.

## 4. Ports

- Postgres: `5433` (identity), `5434` (customer), `5435` (ledger), `5436` (cards), `5437` (notification)
- Redis: `6379`
- RabbitMQ: `5672`, Management UI `15672`
- Grafana: `3000` (admin / `smartbank_dev_only`)
- Prometheus: `9090`, Loki: `3100`, Tempo: `3200`, OTel: `4317/4318`, `8889`, `13133`

## 5. Migrate + seed databases

All 5 APIs auto-migrate on startup in `Development` (`Database.MigrateAsync()`).
Identity/Customer/Cards also seed; Ledger/Notification migrate schema only.
(Cards seed reuses Customer's hardcoded account GUIDs, no cross-DB dependency.)

```powershell
./scripts/migrate-seed.ps1
./scripts/migrate-seed.ps1 -SkipInfra -TimeoutSec 120
```

This builds `SmartBank.slnx`, then boots all 5 APIs in parallel with
`--no-launch-profile` (ports 5101-5105 from `appsettings.json`), waits for
`/health/ready`, stops them. Logs: `.logs/migrate-<service>.log`.

Seed logins (password `123456`): `alex.morgan@smartbank.test`, `jordan.lee@smartbank.test`.

## 6. Start all APIs in background (one command)

```powershell
./scripts/start-apis.ps1   # build + start 5 services + gateway, wait for /health/ready
./scripts/stop-apis.ps1    # stop them
```

Runs `identity 5101`, `customer 5102 (+ gRPC 6102)`, `ledger 5103`, `cards 5104`,
`notification 5105`, `gateway 5100` with `--no-launch-profile` in `Development`.
PIDs: `.pids/apis-*.pid`, logs: `.logs/apis-<name>.log`.
Sets `E2E_OTP_SEAM=true` so Identity uses `InMemoryOtpSink` and maps
`GET /api/auth/e2e/otp` (needed for local login+MFA).

Login + OTP flow (password `123456`):

```powershell
# 1. login -> returns challengeId (mfaRequired: true)
Invoke-WebRequest http://localhost:5101/api/auth/login -Method POST `
  -ContentType 'application/json' `
  -Body '{"email":"alex.morgan@smartbank.test","password":"123456"}' -UseBasicParsing

# 2. fetch OTP code, then verify with challengeId + code
Invoke-WebRequest 'http://localhost:5101/api/auth/e2e/otp?email=alex.morgan@smartbank.test' -UseBasicParsing
```

Note: the OTP endpoint returns `404` until a login has generated a code for that email.

## 7. Frontend (smartbank-web)

### Install dependencies

Requires **Node.js 24** and **npm 11**. Download from [nodejs.org](https://nodejs.org) if not already installed.

```powershell
cd frontend/smartbank-web
npm ci            # first time only (uses package-lock.json)
```

### Run the frontend

Backend gateway must be running (`./scripts/start-apis.ps1`) — the app calls `apiBaseUrl: 'http://localhost:5100'` (`src/environments/environment.ts`).

```powershell
cd frontend/smartbank-web
npm ci            # first time only (uses package-lock.json)
npm start         # ng serve --port 4200
```

Open `http://localhost:4200`.

Other commands (from `frontend/smartbank-web`):

```powershell
npm run build     # production build -> dist/smartbank-web
npm test          # vitest
npm run e2e       # playwright (needs `npx playwright install` first time)
```

## 8. Run tests (in order)

Full local stack must be up first (steps 2, 5, 6, 7):

```powershell
# from repo root
docker compose up -d
./scripts/migrate-seed.ps1
./scripts/start-apis.ps1     # sets E2E_OTP_SEAM=true (e2e needs it)
```

Backend (from repo root):

```powershell
dotnet test SmartBank.slnx
```

Frontend unit tests (from `frontend/smartbank-web`):

```powershell
npm test          # vitest run
```

E2E (from `frontend/smartbank-web`, backend running via `start-apis.ps1`):

```powershell
npx playwright install chromium   # first time only
npm run e2e                       # serves :4200 itself, talks to gateway :5100
```

E2E logs in via UI as `alex.morgan@smartbank.test` / `123456` and fetches the
MFA code from the gateway (`http://localhost:5100/api/auth/e2e/otp?email=...`),
so Identity must run with `E2E_OTP_SEAM=true` — `start-apis.ps1` does this.

## 9. Logs and stop

```powershell
docker compose logs -f
docker compose logs -f rabbitmq redis
docker compose down        # stop, keep data
docker compose down -v     # stop + delete volumes/data
```

## 10. Troubleshooting

- `Internal Server Error for API route ... /info` right after launching Docker Desktop: engine still starting, wait 30-60s and retry `docker info`.
- `docker-desktop` WSL distro `Stopped`: start Docker Desktop, it manages WSL itself. No need to open Ubuntu manually.
- First `up` is slow: pulls ~500MB+ of images.
