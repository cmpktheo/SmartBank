import { test, expect } from '@playwright/test';
import { SettingsPage, loginAsAlex } from './poms/pages';

test('H1 settings shows email, customer id, expiry and MFA enforced', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const settings = new SettingsPage(page);
  await settings.goto();
  await expect(page.getByText('Signed in as')).toBeVisible();
  await expect(page.getByText('alex.morgan@smartbank.test')).toBeVisible();
  await expect(page.getByText('Customer ID')).toBeVisible();
  await expect(page.getByText('Session expires')).toBeVisible();
  await expect(page.getByText('Enforced')).toBeVisible();
});

test('H2 settings Sign out returns to login', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const settings = new SettingsPage(page);
  await settings.goto();
  await settings.signOut().click();
  await expect(page).toHaveURL(/auth\/login/);
});

test('H3 settings compliance line has no mock toggles', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/settings');
  await expect(page.getByText(/high-contrast dark navy/)).toBeVisible();
  await expect(page.getByText(/not offered by the API/)).toBeVisible();
});
