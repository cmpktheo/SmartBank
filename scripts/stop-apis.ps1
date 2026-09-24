<#
.SYNOPSIS
  Stop background APIs started by scripts/start-apis.ps1.
#>
$ErrorActionPreference = 'Continue'
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$files = Get-ChildItem "$RepoRoot/.pids/apis-*.pid" -ErrorAction SilentlyContinue
if (-not $files) { Write-Host 'No .pids/apis-*.pid files - nothing to stop.'; exit 0 }

foreach ($f in $files) {
  $pidValue = (Get-Content $f.FullName -ErrorAction SilentlyContinue | Select-Object -First 1)
  if ($pidValue -match '^\d+$') {
    Write-Host "Stopping $($f.BaseName) (PID $pidValue)..."
    & taskkill /PID $pidValue /T /F 2>$null | Out-Null
  }
  Remove-Item $f.FullName -Force -ErrorAction SilentlyContinue
}
Write-Host 'Done.'
