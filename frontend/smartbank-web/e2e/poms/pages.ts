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

export class TransferPage {
  constructor(private readonly page: Page) {}
  source = () => this.page.getByTestId('transfer-source-select');
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
}

export class CardsPage {
  constructor(private readonly page: Page) {}
  list = () => this.page.getByTestId('card-list-item');
  back = () => this.page.getByTestId('card-back-btn');
  freeze = () => this.page.getByTestId('card-freeze-toggle');
  status = () => this.page.getByTestId('card-status');
  cvvToggle = () => this.page.getByTestId('card-cvv-toggle');
  cvv = () => this.page.getByTestId('card-cvv-value');

  async openFirst() {
    await this.list().first().click();
    await this.freeze().first().waitFor();
  }
}

export async function loginAsAlex(page: Page, request: APIRequestContext) {
  const login = new LoginPage(page);
  await login.goto();
  await login.login('alex.morgan@smartbank.test', '123456');
  const otp = await request.get('http://localhost:5100/api/auth/e2e/otp', {
    params: { email: 'alex.morgan@smartbank.test' },
  });
  const { code } = await otp.json();
  const mfa = new MfaPage(page);
  await mfa.verify(code);
  await expect(page).toHaveURL(/dashboard/);
}
