import { test, expect } from '@playwright/test';
import { LoginPage, MfaPage } from './poms/pages';

test('B1 login submit disabled with invalid email / empty password', async ({ page }) => {
  const login = new LoginPage(page);
  await login.goto();
  await login.email().fill('not-an-email');
  await login.password().fill('123456');
  await expect(login.submit()).toBeDisabled();
  await login.email().fill('alex.morgan@smartbank.test');
  await login.password().fill('');
  await expect(login.submit()).toBeDisabled();
  await login.password().fill('123456');
  await expect(login.submit()).toBeEnabled();
});

test('B2 MFA submit disabled until 6 digits entered', async ({ page }) => {
  const login = new LoginPage(page);
  await login.goto();
  await login.login('alex.morgan@smartbank.test', '123456');
  const mfa = new MfaPage(page);
  await expect(page).toHaveURL(/mfa/);
  await expect(mfa.submit()).toBeDisabled();
  await mfa.code().fill('12345');
  await expect(mfa.submit()).toBeDisabled();
  await mfa.code().fill('123456');
  await expect(mfa.submit()).toBeEnabled();
});

test('B3 MFA Fetch OTP demo reveals 6-digit code', async ({ page }) => {
  const login = new LoginPage(page);
  await login.goto();
  await login.login('alex.morgan@smartbank.test', '123456');
  await expect(page).toHaveURL(/mfa/);
  await page.getByTestId('mfa-fetch-otp-btn').click();
  await expect(page.getByTestId('simulated-sms-otp')).toHaveText(/^\d{6}$/);
  await expect(page.getByTestId('mfa-timer')).toBeVisible();
});
