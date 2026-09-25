<#
.SYNOPSIS
  End-to-end demo journey through the API gateway: login + MFA, accounts,
  transfers, statements, cards, notifications.

.DESCRIPTION
  Exercises the same flows as the Angular SPA (via http://localhost:5100)
  so the Grafana dashboards (RED, Services, Audit) get realistic traffic:
  logins, MFA verifies, transfers, card ops, notifications.

  Flow:
    1. Login as Alex (password) -> MFA challenge -> OTP via the E2E seam
       (E2E_OTP_SEAM=true, set by start-apis.ps1) -> verify -> JWT.
    2. Visit accounts pages (list + detail per account).
    3. Login as Jordan, resolve his IBAN, verify it, transfer Alex -> Jordan
       and Jordan -> Alex (Internal, Idempotency-Key).
    4. Poll the async notification log until the transfer email lands.
    5. Filter transactions: recent, last week, last 3 months, Credit/Debit,
       kind=Transfer, plus a last-month CSV export.
    6. Cards: list, detail, raise limits, freeze -> unfreeze (restores state).
    7. Refresh tokens, one wrong-password login (negative path), logout.

  Leaves no mess behind: the card ends in its original state (unfrozen,
  original limits are NOT restored - new limits are the point).

.EXAMPLE
  ./scripts/demo-journey.ps1
  ./scripts/demo-journey.ps1 -Amount '50.00' -PeerAmount '5.00'
#>
param(
  [string]$Gateway = 'http://localhost:5100',
  [string]$Identity = 'http://localhost:5101',
  [string]$Notifications = 'http://localhost:5105',
  [string]$Email = 'alex.morgan@smartbank.test',
  [string]$Password = '123456',
  [string]$PeerEmail = 'jordan.lee@smartbank.test',
  [string]$PeerPassword = '123456',
  [string]$Amount = '25.00',
  [string]$PeerAmount = '10.00'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

# One correlation id for the whole run: paste it into Tempo / Loki to follow
# the journey across gateway -> services.
$CorrelationId = "demo-journey-$([guid]::NewGuid().ToString('N').Substring(0, 8))"

function Write-Step([string]$Text) {
  Write-Host ''
  Write-Host "== $Text ==" -ForegroundColor Cyan
}

function Write-Ok([string]$Text) {
  Write-Host "   OK $Text" -ForegroundColor Green
}

function Write-Warn([string]$Text) {
  Write-Host "   !! $Text" -ForegroundColor Yellow
}

# Uniform HTTP helper (works on PS 5.1, surfaces status + body on errors).
function Invoke-SbApi {
  param(
    [string]$Method = 'GET',
    [string]$Url,
    [object]$Body = $null,
    [string]$Token = '',
    [hashtable]$ExtraHeaders = @{},
    [switch]$Raw
  )
  $headers = @{ 'X-Correlation-Id' = $CorrelationId }
  foreach ($k in $ExtraHeaders.Keys) { $headers[$k] = $ExtraHeaders[$k] }
  if ($Token) { $headers['Authorization'] = "Bearer $Token" }
  $params = @{
    Uri = $Url; Method = $Method; Headers = $headers
    UseBasicParsing = $true; TimeoutSec = 20
  }
  if ($null -ne $Body) {
    $params['Body'] = ($Body | ConvertTo-Json -Depth 6 -Compress)
    $params['ContentType'] = 'application/json'
  }
  try {
    $r = Invoke-WebRequest @params
    $status = [int]$r.StatusCode
  } catch {
    $resp = $_.Exception.Response
    if ($null -eq $resp) { throw }
    $status = [int]$resp.StatusCode
    $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
    $r = @{ Content = $reader.ReadToEnd() }
  }
  $parsed = $null
  if (-not $Raw -and $r.Content) {
    try { $parsed = $r.Content | ConvertFrom-Json } catch { }
  }
  return @{ Status = $status; Content = [string]$r.Content; Json = $parsed }
}

function Assert-Status([hashtable]$Res, [int[]]$Want, [string]$What) {
  if ($Res.Status -notin $Want) {
    throw "$What failed: HTTP $($Res.Status). Body: $($Res.Content)"
  }
}

function Get-AuthToken([string]$LoginEmail, [string]$LoginPassword) {
  $login = Invoke-SbApi -Method POST -Url "$Gateway/api/auth/login" `
    -Body @{ email = $LoginEmail; password = $LoginPassword }
  Assert-Status $login @(200) "login $LoginEmail"
  if (-not $login.Json.mfaRequired) {
    Write-Ok "$LoginEmail logged in (no MFA)"
    return @{ Access = $login.Json.accessToken; Refresh = $login.Json.refreshToken }
  }
  $challengeId = $login.Json.challengeId
  $otp = Invoke-SbApi -Method GET `
    -Url "$Gateway/api/auth/e2e/otp?email=$([uri]::EscapeDataString($LoginEmail))"
  Assert-Status $otp @(200) "e2e otp $LoginEmail (is E2E_OTP_SEAM=true?)"
  $verify = Invoke-SbApi -Method POST -Url "$Gateway/api/auth/mfa/verify" `
    -Body @{ challengeId = $challengeId; code = $otp.Json.code }
  Assert-Status $verify @(200) "mfa verify $LoginEmail"
  Write-Ok "$LoginEmail logged in (MFA verified)"
  return @{ Access = $verify.Json.accessToken; Refresh = $verify.Json.refreshToken }
}

# --- 0. readiness -----------------------------------------------------------
Write-Step "readiness (CorrelationId=$CorrelationId)"
foreach ($p in 5100, 5101, 5102, 5103, 5104, 5105) {
  $h = Invoke-SbApi -Method GET -Url "http://localhost:$p/health/ready"
  Assert-Status $h @(200) "health :$p"
}
Write-Ok 'all 6 services ready'

# --- 1. login Alex ----------------------------------------------------------
Write-Step "login + MFA ($Email)"
$alex = Get-AuthToken $Email $Password
$me = Invoke-SbApi -Method GET -Url "$Gateway/api/auth/me" -Token $alex.Access
Assert-Status $me @(200) 'auth/me'
Write-Ok "me: $($me.Json.email) customerId=$($me.Json.customerId)"

# --- 2. accounts pages ------------------------------------------------------
Write-Step 'accounts pages (list + detail)'
$acc = Invoke-SbApi -Method GET -Url "$Gateway/api/accounts" -Token $alex.Access
Assert-Status $acc @(200) 'accounts list'
$accounts = @($acc.Json)
foreach ($a in $accounts) {
  Write-Ok "$($a.alias) [$($a.type)] $($a.ibanFormatted) $($a.availableBalance) $($a.currency)"
  $det = Invoke-SbApi -Method GET -Url "$Gateway/api/accounts/$($a.id)" -Token $alex.Access
  Assert-Status $det @(200) "account detail $($a.alias)"
}
$source = $accounts | Where-Object { $_.type -eq 'Current' } | Select-Object -First 1
if (-not $source) { $source = $accounts | Select-Object -First 1 }
if (-not $source) { throw 'Alex has no accounts - is CustomerSeed applied?' }
$balanceBefore = [decimal]$source.availableBalance
Write-Ok "transfer source: $($source.alias) id=$($source.id) balance=$balanceBefore"

# --- 3. peer + transfer -----------------------------------------------------
Write-Step "peer login + transfers ($PeerEmail)"
$peer = Get-AuthToken $PeerEmail $PeerPassword
$peerAcc = Invoke-SbApi -Method GET -Url "$Gateway/api/accounts" -Token $peer.Access
Assert-Status $peerAcc @(200) 'peer accounts'
$peerMain = @($peerAcc.Json) | Select-Object -First 1
$peerIban = ($peerMain.iban -replace '\s', '')
Write-Ok "peer account: $($peerMain.alias) $($peerMain.ibanFormatted)"

$verify = Invoke-SbApi -Method GET -Url "$Gateway/api/accounts/iban/$peerIban" -Token $alex.Access
Assert-Status $verify @(200) 'iban verify'
Write-Ok "iban verified=$($verify.Json.verified) holder=$($verify.Json.accountHolderName)"

$narrative = "Demo journey $CorrelationId"
$t1 = Invoke-SbApi -Method POST -Url "$Gateway/api/transfers" -Token $alex.Access `
  -ExtraHeaders @{ 'Idempotency-Key' = [guid]::NewGuid().ToString() } `
  -Body @{ sourceAccountId = $source.id; destinationIban = $peerIban
    amount = $Amount; currency = $source.currency
    transferType = 'Internal'; narrative = $narrative }
Assert-Status $t1 @(201) 'transfer Alex -> Jordan'
Write-Ok "booked ref=$($t1.Json.reference) tx=$($t1.Json.transactionId)"

$t2 = Invoke-SbApi -Method POST -Url "$Gateway/api/transfers" -Token $peer.Access `
  -ExtraHeaders @{ 'Idempotency-Key' = [guid]::NewGuid().ToString() } `
  -Body @{ sourceAccountId = $peerMain.id; destinationIban = ($source.iban -replace '\s', '')
    amount = $PeerAmount; currency = $peerMain.currency
    transferType = 'Internal'; narrative = "Return leg $CorrelationId" }
Assert-Status $t2 @(201) 'transfer Jordan -> Alex'
Write-Ok "booked ref=$($t2.Json.reference) tx=$($t2.Json.transactionId)"

$accAfter = Invoke-SbApi -Method GET -Url "$Gateway/api/accounts" -Token $alex.Access
$srcAfter = @($accAfter.Json) | Where-Object { $_.id -eq $source.id } | Select-Object -First 1
Write-Ok "source balance $balanceBefore -> $($srcAfter.availableBalance) (net $([decimal]$srcAfter.availableBalance - $balanceBefore))"

# --- 4. async notification --------------------------------------------------
Write-Step 'notification log (async consumer)'
$found = $null
for ($i = 1; $i -le 10; $i++) {
  $log = Invoke-SbApi -Method GET -Url "$Notifications/api/notifications/log?take=10"
  Assert-Status $log @(200) 'notifications log'
  $found = @($log.Json) | Where-Object { $_.Body -like "*$($t1.Json.reference)*" } | Select-Object -First 1
  if ($found) { break }
  Start-Sleep -Seconds 2
}
if ($found) {
  Write-Ok "[$($found.template)/$($found.channel)] to $($found.recipient): $($found.subject)"
} else {
  Write-Warn 'transfer notification not visible yet - check RabbitMQ consumer / notification service logs'
}

# --- 5. statements & filters ------------------------------------------------
Write-Step 'statements & filters'
$acctId = $source.id
function Get-Tx([string]$Query, [string]$Label) {
  $r = Invoke-SbApi -Method GET -Url "$Gateway/api/ledger/accounts/$acctId/transactions$Query" -Token $alex.Access
  Assert-Status $r @(200) $Label
  Write-Ok "$Label -> total=$($r.Json.total) returned=$(@($r.Json.items).Count)"
  return $r.Json
}
$recent = Invoke-SbApi -Method GET -Url "$Gateway/api/ledger/accounts/$acctId/recent?limit=5" -Token $alex.Access
Assert-Status $recent @(200) 'recent'
Write-Ok "recent(5) -> $(@($recent.Json).Count) rows"

$null = Get-Tx '?page=1&pageSize=5&type=All&kind=' 'all (page 1)'
$week = (Get-Date).AddDays(-7).ToUniversalTime().ToString('o')
$null = Get-Tx "?page=1&pageSize=20&type=All&kind=&from=$([uri]::EscapeDataString($week))" 'last week'
$quarter = (Get-Date).AddMonths(-3).ToUniversalTime().ToString('o')
$null = Get-Tx "?page=1&pageSize=20&type=All&kind=&from=$([uri]::EscapeDataString($quarter))" 'last 3 months'
$null = Get-Tx '?page=1&pageSize=20&type=Credit&kind=' 'credits only'
$null = Get-Tx '?page=1&pageSize=20&type=Debit&kind=' 'debits only'
$null = Get-Tx '?page=1&pageSize=20&type=All&kind=Transfer' 'kind=Transfer'

$month = (Get-Date).AddMonths(-1).ToUniversalTime().ToString('o')
$csv = Invoke-SbApi -Method GET `
  -Url "$Gateway/api/ledger/accounts/$acctId/statement.csv?from=$([uri]::EscapeDataString($month))" `
  -Token $alex.Access -Raw
Assert-Status $csv @(200) 'statement csv'
$csvPath = Join-Path $RepoRoot (".logs/statement-$($source.alias)-last-month.csv")
$csv.Content | Set-Content -Path $csvPath -Encoding UTF8
Write-Ok "csv saved: $csvPath ($(($csv.Content -split "`n").Count - 1) rows)"

# --- 6. cards ---------------------------------------------------------------
Write-Step 'cards (limits + freeze cycle)'
$cards = Invoke-SbApi -Method GET -Url "$Gateway/api/cards" -Token $alex.Access
Assert-Status $cards @(200) 'cards list'
$cardList = @($cards.Json)
if ($cardList.Count -eq 0) {
  Write-Warn 'no cards for Alex - skipping card steps (is CardsSeed applied?)'
} else {
  $card = $cardList | Select-Object -First 1
  Write-Ok "card $($card.brand) $($card.type) $($card.maskedPan) status=$($card.status) ecom=$($card.dailyEcommerceLimit) atm=$($card.dailyAtmLimit)"
  $cd = Invoke-SbApi -Method GET -Url "$Gateway/api/cards/$($card.id)" -Token $alex.Access
  Assert-Status $cd @(200) 'card detail'

  # Card limits are capped server-side (ecom 0-10000, ATM 0-2000): nudge up,
  # but back off (decrease) when the step would breach the cap, so repeated
  # runs never 400 and the card keeps moving instead of pinning at the ceiling.
  function Move-Limit([decimal]$Current, [decimal]$Step, [decimal]$Min, [decimal]$Max) {
    if ($Current + $Step -le $Max) { return $Current + $Step }
    if ($Current - $Step -ge $Min) { return $Current - $Step }
    return $Current
  }
  $newEcom = (Move-Limit ([decimal]$card.dailyEcommerceLimit) 500 0 10000).ToString('0.00', [cultureinfo]::InvariantCulture)
  $newAtm = (Move-Limit ([decimal]$card.dailyAtmLimit) 100 0 2000).ToString('0.00', [cultureinfo]::InvariantCulture)
  $lim = Invoke-SbApi -Method PATCH -Url "$Gateway/api/cards/$($card.id)/limits" -Token $alex.Access `
    -Body @{ dailyEcommerceLimit = $newEcom; dailyAtmLimit = $newAtm }
  Assert-Status $lim @(200) 'set limits'
  Write-Ok "limits ecom $($card.dailyEcommerceLimit) -> $($lim.Json.dailyEcommerceLimit), atm $($card.dailyAtmLimit) -> $($lim.Json.dailyAtmLimit)"

  try {
    $fr = Invoke-SbApi -Method POST -Url "$Gateway/api/cards/$($card.id)/freeze" -Token $alex.Access `
      -ExtraHeaders @{ 'Idempotency-Key' = [guid]::NewGuid().ToString() } -Body @{}
    Assert-Status $fr @(200) 'freeze'
    Write-Ok "freeze -> status=$($fr.Json.status)"
  } finally {
    $un = Invoke-SbApi -Method POST -Url "$Gateway/api/cards/$($card.id)/unfreeze" -Token $alex.Access `
      -ExtraHeaders @{ 'Idempotency-Key' = [guid]::NewGuid().ToString() } -Body @{}
    Assert-Status $un @(200) 'unfreeze'
    Write-Ok "unfreeze -> status=$($un.Json.status)"
  }
}

# --- 7. refresh / negative / logout -----------------------------------------
Write-Step 'refresh, negative login, logout'
$ref = Invoke-SbApi -Method POST -Url "$Gateway/api/auth/refresh" `
  -Body @{ refreshToken = $alex.Refresh }
Assert-Status $ref @(200) 'token refresh'
$alex.Access = $ref.Json.accessToken
Write-Ok "refreshed, new access token ...$($alex.Access.Substring([Math]::Max(0, $alex.Access.Length - 8)))"

$bad = Invoke-SbApi -Method POST -Url "$Gateway/api/auth/login" `
  -Body @{ email = $Email; password = 'Wrong1!' }
if ($bad.Status -eq 200) {
  Write-Warn 'wrong password was ACCEPTED - check Identity lockout/validation!'
} else {
  Write-Ok "wrong password rejected: HTTP $($bad.Status)"
}

$out = Invoke-SbApi -Method POST -Url "$Gateway/api/auth/logout" -Token $alex.Access `
  -Body @{ refreshToken = $ref.Json.refreshToken }
if ($out.Status -eq 204) {
  Write-Ok 'logged out (204)'
  $probe = Invoke-SbApi -Method GET -Url "$Gateway/api/auth/me" -Token $alex.Access
  if ($probe.Status -eq 401) { Write-Ok 'token blacklisted at gateway (401 as expected)' }
  else { Write-Warn "post-logout /me returned $($probe.Status) (blacklist may be off)" }
} else {
  Write-Warn "logout returned $($out.Status): $($out.Content)"
}

Write-Host ''
Write-Host 'Journey complete.' -ForegroundColor Green
Write-Host "CorrelationId for Tempo/Loki: $CorrelationId"
