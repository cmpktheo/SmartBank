<#
.SYNOPSIS
  Migrate + seed all SmartBank databases by booting each API briefly in Development.

.DESCRIPTION
  All 5 services call Database.MigrateAsync() on startup when
  ASPNETCORE_ENVIRONMENT=Development (Identity also in Testing).
  Identity/Customer/Cards also run their *Seed classes after migrate.
  Ledger/Notification migrate schema only (no seed).

  This script boots all 5 APIs in parallel with --no-launch-profile (so Kestrel
  ports come from appsettings.json: 5101-5105), waits for /health/ready,
  then stops them. By the time health is green, migrate+seed has completed.

  The services are independent (each migrates its own DB; seeds use hardcoded
  matching GUIDs, no cross-DB reads), so parallel boot is safe and ~5x faster.

.EXAMPLE
  ./scripts/migrate-seed.ps1
  ./scripts/migrate-seed.ps1 -SkipInfra -TimeoutSec 120
#>
param(
  [int]$TimeoutSec = 120,
  [switch]$SkipInfra
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$Services = @(
  @{ Name = 'identity';     Project = 'src/Services/Identity/SmartBank.Identity.Api/SmartBank.Identity.Api.csproj';         Health = 'http://localhost:5101/health/ready' },
  @{ Name = 'customer';     Project = 'src/Services/Customer/SmartBank.Customer.Api/SmartBank.Customer.Api.csproj';         Health = 'http://localhost:5102/health/ready' },
  @{ Name = 'ledger';       Project = 'src/Services/Ledger/SmartBank.Ledger.Api/SmartBank.Ledger.Api.csproj';               Health = 'http://localhost:5103/health/ready' },
  @{ Name = 'cards';        Project = 'src/Services/Cards/SmartBank.Cards.Api/SmartBank.Cards.Api.csproj';                 Health = 'http://localhost:5104/health/ready' },
  @{ Name = 'notification'; Project = 'src/Services/Notification/SmartBank.Notification.Api/SmartBank.Notification.Api.csproj'; Health = 'http://localhost:5105/health/ready' }
)

function Wait-TcpPort {
  param([string]$Host_, [int]$Port, [int]$TimeoutSec_ = 60)
  $deadline = (Get-Date).AddSeconds($TimeoutSec_)
  while ((Get-Date) -lt $deadline) {
    try {
      $c = New-Object Net.Sockets.TcpClient
      $iar = $c.BeginConnect($Host_, $Port, $null, $null)
      if ($iar.AsyncWaitHandle.WaitOne(500) -and $c.Connected) { $c.Close(); return }
      $c.Close()
    } catch { }
    Start-Sleep -Milliseconds 500
  }
  throw "Timed out waiting for ${Host_}:${Port}"
}

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

# 1. Infra must be up (Postgres 5433-5437, Redis, RabbitMQ).
if (-not $SkipInfra) {
  Write-Host '== docker compose up -d ==' -ForegroundColor Cyan
  docker compose up -d
  Write-Host 'Waiting for Postgres ports 5433-5437...' -ForegroundColor Cyan
  foreach ($p in 5433..5437) { Wait-TcpPort -Host_ 'localhost' -Port $p -TimeoutSec_ 90 }
  Wait-TcpPort -Host_ 'localhost' -Port 6379 -TimeoutSec_ 60
  Wait-TcpPort -Host_ 'localhost' -Port 5672 -TimeoutSec_ 90
} else {
  Write-Host '== Skipping infra (assumed already up) ==' -ForegroundColor Yellow
}

# 2. Kill stale dotnet processes so build can overwrite locked DLLs.
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# 3. Fail fast on compile errors before booting anything.
Write-Host '== dotnet build SmartBank.slnx ==' -ForegroundColor Cyan
dotnet build SmartBank.slnx -c Debug --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }

New-Item -ItemType Directory -Path "$RepoRoot/.logs" -Force | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'

foreach ($svc in $Services) {
  $logOut = "$RepoRoot/.logs/migrate-$($svc.Name).log"
  $logErr = "$RepoRoot/.logs/migrate-$($svc.Name).err.log"
  Write-Host "== starting $($svc.Name) (migrate+seed) ==" -ForegroundColor Cyan

  $proc = Start-Process -FilePath 'dotnet' `
    -ArgumentList "run --project `"$($svc.Project)`" --no-launch-profile --no-build" `
    -NoNewWindow -PassThru `
    -RedirectStandardOutput $logOut -RedirectStandardError $logErr
  # Stash the wrapper PID on the object for the wait/stop loops below.
  $svc | Add-Member -NotePropertyName Pid -NotePropertyValue $proc.Id -Force
  $svc | Add-Member -NotePropertyName Proc -NotePropertyValue $proc -Force
}

$failed = @()
foreach ($svc in $Services) {
  try {
    Wait-HttpReady -Url $svc.Health -TimeoutSec_ $TimeoutSec
    Write-Host "   $($svc.Name): READY ($($svc.Health))" -ForegroundColor Green
  } catch {
    $failed += $svc.Name
    $logOut = "$RepoRoot/.logs/migrate-$($svc.Name).log"
    $logErr = "$RepoRoot/.logs/migrate-$($svc.Name).err.log"
    Write-Host "   $($svc.Name): FAILED - tail of $logOut :" -ForegroundColor Red
    if (Test-Path $logOut) { Get-Content $logOut -Tail 40 }
    if (Test-Path $logErr) { Get-Content $logErr -Tail 20 }
  } finally {
    # Stop the API and its child dotnet process tree.
    try { & taskkill /PID $svc.Pid /T /F 2>$null | Out-Null } catch { }
    try { Stop-Process -Id $svc.Pid -Force -ErrorAction SilentlyContinue } catch { }
    try { $svc.Proc.WaitForExit(10000) | Out-Null } catch { }
  }
}
if ($failed.Count -gt 0) { throw "Migrate+seed failed for: $($failed -join ', ')" }

Write-Host 'All services migrated + seeded.' -ForegroundColor Green
Write-Host 'Seed logins (password 123456): alex.morgan@smartbank.test, jordan.lee@smartbank.test'
