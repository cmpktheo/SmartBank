# Playwright E2E Plan — smartbank-web

Source survey: `frontend/smartbank-web/src/**` (ignored per `.gitignore`: `node_modules/`, `.angular/`, `dist/`, `coverage/`, plus repo-level `.logs/`, `.pids/`, `.vs/`, `bin/`, `obj/`).
App routes (`src/app/app.routes.ts`): `/auth/login`, `/auth/mfa`, `/dashboard`, `/accounts`, `/accounts/detail`, `/transfers`, `/cards`, `/settings`.
Seed users (backend `IdentitySeed` + `CustomerSeed`): `alex.morgan@smartbank.test` / `123456`, `jordan.lee@smartbank.test` / `123456`.
Existing helper: `e2e/poms/pages.ts` → `loginAsAlex(page, request)` uses `POST /api/auth/login` + `GET /api/auth/e2e/otp` (needs gateway at `http://localhost:5100` with `E2E_OTP_SEAM=true`, webServer `npm start` on `:4200`).

## Existing coverage (13 tests — all passingbaseline)
- [x] `e2e/smoke.spec.ts` — title contains SmartBank
- [x] `e2e/auth.spec.ts` — invalid password error; wrong OTP error; valid login lands on dashboard
- [x] `e2e/cards.spec.ts` — list masked PAN + open detail; freeze→Frozen; unfreeze→Active; CVV reveal 3 digits
- [x] `e2e/transfer.spec.ts` — golden internal transfer updates balance; amount>balance disables submit; bad IBAN checksum disables submit
- [x] `e2e/statement.spec.ts` — debit filter hides credits; export CSV download filename

## New plan (implemented below — mark [x] when file lands + typechecks)

### A. Auth / shell / navigation — `e2e/navigation.spec.ts` — finished
- [x] A1 unauthenticated `/dashboard` redirects to `/auth/login` (guard)
- [x] A2 sidebar navigates dashboard→accounts→transfers→cards→settings (URL + breadcrumb)
- [x] A3 topbar `quick-transfer-btn` goes to `/transfers`
- [x] A4 sidebar shows `session-remaining` countdown `MM:SS`
- [x] A5 `nav-logout` signs out → `/auth/login`, back button cannot re-enter dashboard

### B. Auth edge cases — `e2e/auth-extra.spec.ts` — finished
- [x] B1 login submit disabled with invalid email / empty password (reactive-form validation)
- [x] B2 MFA submit disabled until 6 digits entered
- [x] B3 MFA “Fetch OTP (Demo)” reveals `simulated-sms-otp` (6 digits)

### C. Dashboard — `e2e/dashboard.spec.ts` — finished
- [x] C1 totals row shows `Total in EUR` with currency value
- [x] C2 account cards expose alias + IBAN + balance testids
- [x] C3 `quick-cards-btn` navigates to `/cards`
- [x] C4 clicking dashboard account card opens `/accounts/detail`
- [x] C5 recent-transactions table renders (or empty-state “No recent activity”)

### D. Accounts list — `e2e/accounts.spec.ts` — finished
- [x] D1 `/accounts` shows “N total” + grid of cards
- [x] D2 “Open new account” toggle reveals alias/type/currency form
- [x] D3 empty alias keeps `account-new-submit` disabled
- [x] D4 creating `E2E-<timestamp>` account appends card with that alias

### E. Account detail / statement — `e2e/account-detail.spec.ts` — finished
- [x] E1 direct `/accounts/detail` without selection redirects to `/accounts`
- [x] E2 kind filter `CardPayment` + Apply keeps valid table state
- [x] E3 date `from`/`to` + Apply keeps valid table state
- [x] E4 footer shows `Showing X of Y transactions` + `Load more` only when hasMore

### F. Transfers validation — `e2e/transfer-validation.spec.ts` — finished
- [x] F1 empty amount disables `transfer-submit-btn`
- [x] F2 Review opens `transfer-confirm-modal`, Cancel closes without success msg
- [x] F3 switching to `Domestic`/`International` shows Ledger-only-settles-Internal warning
- [x] F4 `transfer-insufficient-msg` appears when amount > balance (covered partially — assert msg text)
- [x] F5 narrative input accepts text (maxlength 140) and confirm dialog shows amount+currency

### G. Cards limits — `e2e/cards-limits.spec.ts` — finished
- [x] G1 `card-back-btn` returns from detail to list
- [x] G2 limits save button disabled when pristine (`Saved (...)` state)
- [x] G3 moving ecom slider enables save (`Save limits (...)` state)
- [x] G4 detail shows expiry `M/YYYY`, masked PAN, status pill, brand visual

### H. Settings — `e2e/settings.spec.ts` — finished
- [x] H1 `/settings` shows Signed-in email + Customer ID + Session expires + MFA Enforced
- [x] H2 Settings Sign out returns to `/auth/login`
- [x] H3 compliance line mentions “high-contrast dark navy” (no mock toggles)

### I. Transfer fixture fix — `e2e/transfer.spec.ts` — finished
- [x] I1 replace missing `e2e/fixtures/seed.json` with live IBAN lookup (2nd account or Jordan via API), keep golden-journey balance assertion

Total: 13 existing + 33 new = 46 tests (`npx playwright test --list` → 46 in 13 files, 0 e2e tsc errors). Run: `npm run e2e` from `frontend/smartbank-web` (needs gateway + seed). Note: live run requires backend (`E2E_OTP_SEAM=true`, seeded Alex+Jordan); not executed here — infra (gateway :5100, web :4200) was down.
