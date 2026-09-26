import { Page, expect, APIRequestContext } from '@playwright/test';

export class LoginPage {
  constructor(private readonly page: Page) {}

  email = () => this.page.getByTestId('login-email-input');
  password = () => this.page.getByTestId('login-password-input');
  submit = () => this.page.getByTestId('login-submit-btn');
  error = () => this.page.getByTestId('login-error');

  async goto() {
    await this.page.goto('/auth/login');
  }

  async login(email: string, password: string) {
    await this.email().fill(email);
    await this.password().fill(password);
    await this.submit().click();
  }
}

export class MfaPage {
  constructor(private readonly page: Page) {}
  code = () => this.page.getByTestId('mfa-code-input');
  submit = () => this.page.getByTestId('mfa-submit-btn');
  resend = () => this.page.getByTestId('mfa-resend-btn');
  timer = () => this.page.getByTestId('mfa-timer');

  async verify(otp: string) {
    await this.code().fill(otp);
    await this.submit().click();
  }
}

export class DashboardPage {
  constructor(private readonly page: Page) {}
  accountCards = () => this.page.getByTestId('dashboard-account-card');
  balances = () => this.page.getByTestId('dashboard-account-balance');
  quickTransfer = () => this.page.getByTestId('quick-transfer-btn');
  quickCards = () => this.page.getByTestId('quick-cards-btn');
  recent = () => this.page.getByTestId('recent-transaction-item');

  async expectLoaded() {
    await expect(this.accountCards().first()).toBeVisible();
  }

  async balanceText(index = 0) {
    return (await this.accountCards().nth(index).getByTestId('dashboard-account-balance').innerText()).trim();
  }
}

export class AccountListPage {
  constructor(private readonly page: Page) {}

  accountCards = () => this.page.getByTestId('dashboard-account-card');
  accountAliases = () => this.page.getByTestId('dashboard-account-alias');
  accountIbans = () => this.page.getByTestId('dashboard-account-iban');
  accountBalances = () => this.page.getByTestId('dashboard-account-balance');
  openNewAccountBtn = () => this.page.getByRole('button', { name: /open new account/i });
  aliasInput = () => this.page.getByTestId('account-new-alias');
  typeSelect = () => this.page.getByTestId('account-new-type');
  currencySelect = () => this.page.getByTestId('account-new-currency');
  submitBtn = () => this.page.getByTestId('account-new-submit');
  error = () => this.page.getByTestId('account-new-error');

  async expectLoaded() {
    await expect(this.accountCards().first()).toBeVisible();
  }

  async accountCount() {
    return this.accountCards().count();
  }

  async aliasText(index = 0) {
    return (await this.accountAliases().nth(index).innerText()).trim();
  }

  async ibanText(index = 0) {
    return (await this.accountIbans().nth(index).innerText()).trim();
  }

  async balanceText(index = 0) {
    return (await this.accountBalances().nth(index).innerText()).trim();
  }
}

export class AccountDetailPage {
  constructor(private readonly page: Page) {}

  backLink = () => this.page.getByRole('link', { name: /back to accounts/i });
  exportBtn = () => this.page.getByTestId('export-statement-btn');
  fromInput = () => this.page.getByTestId('statement-filter-from');
  toInput = () => this.page.getByTestId('statement-filter-to');
  typeSelect = () => this.page.getByTestId('statement-filter-type');
  applyBtn = () => this.page.getByTestId('statement-apply-btn');
  kindSelect = () => this.page.getByTestId('statement-kind');
  statementRows = () => this.page.getByTestId('statement-row');
  balanceCells = () => this.page.getByTestId('statement-balance');
  loadMoreBtn = () => this.page.getByTestId('statement-load-more-btn');

  async expectLoaded() {
    await expect(this.statementRows().first()).toBeVisible();
  }

  async rowCount() {
    return this.statementRows().count();
  }

  async balanceText(index = 0) {
    return (await this.balanceCells().nth(index).innerText()).trim();
  }
}

export class TransferPage {
  constructor(private readonly page: Page) {}
  source = () => this.page.getByTestId('transfer-source-select');
  sourceOptions = () => this.page.locator('[data-testid^="transfer-source-option-"]');
  iban = () => this.page.getByTestId('transfer-iban-input');
  amount = () => this.page.getByTestId('transfer-amount-input');
  submit = () => this.page.getByTestId('transfer-submit-btn');
  confirm = () => this.page.getByTestId('transfer-confirm-btn');
  success = () => this.page.getByTestId('transfer-success-msg');
  reference = () => this.page.getByTestId('transfer-reference');
  verified = () => this.page.getByTestId('iban-verified-badge');

  async goto() { await this.page.goto('/transfers'); }

  async fillInternal(iban: string, amount: string) {
    await this.iban().fill(iban);
    await this.amount().fill(amount);
  }

  async submitAndConfirm() {
    await this.submit().click();
    await expect(this.page.getByTestId('transfer-confirm-modal')).toBeVisible();
    await this.confirm().click();
  }

  /**
   * Selects a source account by id. Needed because other suites create empty
   * E2E-* accounts that may sort first, so the default selection is not
   * reliable (empty balance disables submit).
   */
  async selectSourceById(id: string) {
    await this.source().click();
    await this.page.getByTestId(`transfer-source-option-${id}`).click();
  }
}

export class CardsPage {
  constructor(private readonly page: Page) {}
  list = () => this.page.getByTestId('card-list-item');
  back = () => this.page.getByTestId('card-back-btn');
  freeze = () => this.page.getByTestId('card-freeze-toggle');
  status = () => this.page.getByTestId('card-status');
  cvvToggle = () => this.page.getByTestId('card-cvv-toggle');
  cvv = () => this.page.getByTestId('card-cvv-value');
  maskedPan = () => this.page.getByTestId('card-masked-pan');
  expiry = () => this.page.getByTestId('card-expiry');
  detail = () => this.page.getByTestId('card-detail');
  limitSave = () => this.page.getByTestId('card-limit-save-btn');
  ecomSlider = () => this.page.getByTestId('card-limit-slider');

  async openFirst() {
    await this.list().first().click();
    await this.freeze().first().waitFor();
  }
}

export class SettingsPage {
  constructor(private readonly page: Page) {}
  async goto() {
    await this.page.goto('/settings');
  }
  signOut = () => this.page.getByTestId('nav-logout').last();
}

export async function loginAsAlex(page: Page, request: APIRequestContext) {
  const login = new LoginPage(page);
  await login.goto();
  await login.login('alex.morgan@smartbank.test', '123456');
  // Wait for the login POST to complete (frontend navigates to MFA only on
  // mfaRequired). Fetching the OTP earlier races the login and returns the
  // previous code from the backend sink, which verify then rejects.
  await expect(page).toHaveURL(/auth\/mfa/);
  const otp = await request.get('http://localhost:5100/api/auth/e2e/otp', {
    params: { email: 'alex.morgan@smartbank.test' },
  });
  const { code } = await otp.json();
  const mfa = new MfaPage(page);
  await mfa.verify(code);
  await expect(page).toHaveURL(/dashboard/);
}

/** Reads the logged-in access token from session storage for API calls. */
export async function getApiToken(page: Page): Promise<string | null> {
  return page.evaluate(() => {
    try {
      const raw = sessionStorage.getItem('sb.tokens');
      return raw ? (JSON.parse(raw).accessToken as string) : null;
    } catch {
      return null;
    }
  });
}

export interface ApiAccount {
  id: string;
  alias?: string;
  iban?: string;
  ibanFormatted?: string;
  currency?: string;
  availableBalance?: string;
}

/** Lists accounts via the API in UI order (dashboard cards and source options follow it). */
export async function getApiAccounts(
  request: APIRequestContext,
  page: Page
): Promise<ApiAccount[]> {
  const token = await getApiToken(page);
  const res = await request.get('http://localhost:5100/api/accounts', {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  const accounts = await res.json();
  if (!Array.isArray(accounts) || accounts.length < 2) {
    throw new Error('Need >=2 accounts for internal transfer test — seed provides Alex(2)+Jordan(1)');
  }
  return accounts as ApiAccount[];
}

function parseMoney(v: string | undefined): number {
  return Number((v ?? '').replace(/,/g, '')) || 0;
}

/**
 * Picks a booking-compatible pair from the caller's own accounts: same
 * currency (the API rejects cross-currency transfers), source holding at
 * least `amount` (the UI disables submit on insufficient funds), source and
 * destination different accounts. `sourceIndex` matches the dashboard card
 * index (same API order) for balance assertions.
 */
export async function getTransferPair(
  request: APIRequestContext,
  page: Page,
  amount: number
): Promise<{ destIban: string; sourceId: string; sourceIndex: number; sourceBalance: number }> {
  const accounts = await getApiAccounts(request, page);
  const groups = new Map<string, ApiAccount[]>();
  for (const a of accounts) {
    const key = a.currency ?? '';
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key)!.push(a);
  }
  for (const group of groups.values()) {
    if (group.length < 2) continue;
    const source = group.find((a) => parseMoney(a.availableBalance) >= amount);
    const dest = source && group.find((a) => a.id !== source.id);
    const destIban = dest && (dest.iban ?? dest.ibanFormatted);
    if (source && dest && destIban) {
      return {
        destIban,
        sourceId: source.id,
        sourceIndex: accounts.findIndex((a) => a.id === source.id),
        sourceBalance: parseMoney(source.availableBalance),
      };
    }
  }
  throw new Error(
    `No same-currency pair found where the source holds at least ${amount}`
  );
}

/** Returns an IBAN belonging to a *different* account than the first one, for internal transfers. */
export async function getInternalDestinationIban(request: APIRequestContext, page: Page): Promise<string> {
  const accounts = await getApiAccounts(request, page);
  // Prefer Jordan's account when present, else any non-first account.
  const jordan = accounts.find((a: { alias?: string }) => /jordan/i.test(a.alias ?? ''));
  const dest = jordan ?? accounts[1];
  return (dest.iban ?? dest.ibanFormatted ?? (dest as { destinationIban?: string }).destinationIban) as string;
}
