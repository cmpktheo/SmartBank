<#
.SYNOPSIS
  Start all SmartBank APIs in the background with one command.

.DESCRIPTION
  Starts 5 services + gateway via `dotnet run --no-launch-profile`
  (ports from appsettings.json), each as a background process.
  Waits for /health/ready, saves PIDs to .pids/, logs to .logs/.
  Stays in the same terminal - no new windows.

.EXAMPLE
  ./scripts/start-apis.ps1
  ./scripts/stop-apis.ps1
#>
param(
  [int]$TimeoutSec = 120,
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$Services = @(
  @{ Name = 'identity';     Project = 'src/Services/Identity/SmartBank.Identity.Api/SmartBank.Identity.Api.csproj';         Health = 'http://localhost:5101/health/ready' },
  @{ Name = 'customer';     Project = 'src/Services/Customer/SmartBank.Customer.Api/SmartBank.Customer.Api.csproj';         Health = 'http://localhost:5102/health/ready' },
  @{ Name = 'ledger';       Project = 'src/Services/Ledger/SmartBank.Ledger.Api/SmartBank.Ledger.Api.csproj';               Health = 'http://localhost:5103/health/ready' },
  @{ Name = 'cards';        Project = 'src/Services/Cards/SmartBank.Cards.Api/SmartBank.Cards.Api.csproj';                 Health = 'http://localhost:5104/health/ready' },
  @{ Name = 'notification'; Project = 'src/Services/Notification/SmartBank.Notification.Api/SmartBank.Notification.Api.csproj'; Health = 'http://localhost:5105/health/ready' },
  @{ Name = 'gateway';      Project = 'src/Gateways/SmartBank.ApiGateway/SmartBank.ApiGateway.csproj';                     Health = 'http://localhost:5100/health/ready' }
)

function Wait-HttpReady {
  param([string]$Url, [int]$TimeoutSec_ = 120)
  $deadline = (Get-Date).AddSeconds($TimeoutSec_)
  while ((Get-Date) -lt $deadline) {
    try {
      $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
      if ($r.StatusCode -eq 200) { return }
    } catch { }
    Start-Sleep -Seconds 1
  }
  throw "Timed out waiting for $Url"
}

# Stop any previous run started by this script (stale PIDs).
if (Test-Path "$RepoRoot/.pids") {
  Get-ChildItem "$RepoRoot/.pids/apis-*.pid" -ErrorAction SilentlyContinue | ForEach-Object {
    $oldPid = (Get-Content $_.FullName -ErrorAction SilentlyContinue | Select-Object -First 1)
    if ($oldPid -match '^\d+$') {
      try { & taskkill /PID $oldPid /T /F 2>$null | Out-Null } catch { }
    }
    Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
  }
}

if (-not $NoBuild) {
  Write-Host '== dotnet build SmartBank.slnx ==' -ForegroundColor Cyan
  dotnet build SmartBank.slnx -c Debug --nologo -v minimal
  if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
}

New-Item -ItemType Directory -Path "$RepoRoot/.logs" -Force | Out-Null
New-Item -ItemType Directory -Path "$RepoRoot/.pids" -Force | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
# OTel: apps only export metrics/traces when this is set
# (AddSmartBankOpenTelemetry skips the OTLP exporter otherwise,
# which leaves Prometheus/Grafana/Tempo with no data).
# Collector OTLP gRPC is mapped to host localhost:4317 (see docker-compose.yml).
$env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://localhost:4317'
# E2E OTP seam: Identity only maps GET /api/auth/e2e/otp and uses
# InMemoryOtpSink when this is "true" (launch profiles set it, but we
# bypass them with --no-launch-profile, so set it here explicitly).
$env:E2E_OTP_SEAM = 'true'

foreach ($svc in $Services) {
  $logOut = "$RepoRoot/.logs/apis-$($svc.Name).log"
  $logErr = "$RepoRoot/.logs/apis-$($svc.Name).err.log"
  if (Test-Path $logOut) { Remove-Item $logOut -Force }
  if (Test-Path $logErr) { Remove-Item $logErr -Force }

  Write-Host "== starting $($svc.Name) ==" -ForegroundColor Cyan
  $proc = Start-Process -FilePath 'dotnet' `
    -ArgumentList "run --project `"$($svc.Project)`" --no-launch-profile --no-build" `
    -NoNewWindow -PassThru `
    -RedirectStandardOutput $logOut -RedirectStandardError $logErr
  Set-Content -Path "$RepoRoot/.pids/apis-$($svc.Name).pid" -Value $proc.Id
}

$failed = @()
foreach ($svc in $Services) {
  try {
    Wait-HttpReady -Url $svc.Health -TimeoutSec_ $TimeoutSec
    Write-Host "   $($svc.Name): READY ($($svc.Health))" -ForegroundColor Green
  } catch {
    $failed += $svc.Name
    Write-Host "   $($svc.Name): NOT READY ($($svc.Health))" -ForegroundColor Red
  }
}

if ($failed.Count -gt 0) {
  Write-Host 'Some services failed. Tails:' -ForegroundColor Red
  foreach ($n in $failed) {
    Write-Host "--- .logs/apis-$n.log (tail) ---" -ForegroundColor Yellow
    if (Test-Path "$RepoRoot/.logs/apis-$n.log") { Get-Content "$RepoRoot/.logs/apis-$n.log" -Tail 30 }
    if (Test-Path "$RepoRoot/.logs/apis-$n.err.log") { Get-Content "$RepoRoot/.logs/apis-$n.err.log" -Tail 10 }
  }
  throw "Failed services: $($failed -join ', '). See .logs/apis-*.log. Stop with ./scripts/stop-apis.ps1"
}

Write-Host ''
Write-Host 'All APIs running in background.' -ForegroundColor Green
Write-Host '  Gateway:      http://localhost:5100/health/ready'
Write-Host '  Identity:     http://localhost:5101/health/ready'
Write-Host '  Customer:     http://localhost:5102/health/ready (+ gRPC 6102)'
Write-Host '  Ledger:       http://localhost:5103/health/ready'
Write-Host '  Cards:        http://localhost:5104/health/ready'
Write-Host '  Notification: http://localhost:5105/health/ready'
Write-Host 'Logs: .logs/apis-<name>.log | Stop: ./scripts/stop-apis.ps1'
