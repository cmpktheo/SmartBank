import { test, expect } from '@playwright/test';
import { LoginPage, MfaPage, loginAsAlex } from './poms/pages';

test('invalid password shows login error and stays on login', async ({ page }) => {
  const login = new LoginPage(page);
  await login.goto();
  await login.login('alex.morgan@smartbank.test', 'Wrong1!');
  await expect(login.error()).toBeVisible();
  await expect(page).toHaveURL(/login/);
});

test('valid password then wrong otp shows mfa error', async ({ page }) => {
  const login = new LoginPage(page);
  await login.goto();
  await login.login('alex.morgan@smartbank.test', '123456');
  const mfa = new MfaPage(page);
  await mfa.verify('000000');
  await expect(page.getByTestId('mfa-error')).toBeVisible();
});

test('valid password then valid otp lands on dashboard with account cards', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await expect(page.getByTestId('dashboard-account-card').first()).toBeVisible();
});
