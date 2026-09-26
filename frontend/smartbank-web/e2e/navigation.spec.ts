import { test, expect } from '@playwright/test';
import { loginAsAlex } from './poms/pages';

test('A1 unauthenticated /dashboard redirects to /auth/login', async ({ page }) => {
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/auth\/login/);
});

test('A2 sidebar navigates through all sections', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const links: Array<[string, RegExp]> = [
    ['nav-accounts', /accounts/],
    ['nav-transfers', /transfers/],
    ['nav-cards', /cards/],
    ['nav-settings', /settings/],
    ['nav-dashboard', /dashboard/],
  ];
  for (const [testid, url] of links) {
    await page.getByTestId(testid).click();
    await expect(page).toHaveURL(url);
  }
});

test('A3 topbar quick-transfer goes to /transfers', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/dashboard');
  await page.getByTestId('topbar-transfer-btn').click();
  await expect(page).toHaveURL(/transfers/);
});

test('A4 sidebar shows session countdown MM:SS', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await expect(page.getByTestId('session-remaining')).toHaveText(/^\d{2}:\d{2}$/);
});

test('A5 logout returns to login and guard blocks dashboard', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.getByTestId('nav-logout').first().click();
  await expect(page).toHaveURL(/auth\/login/);
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/auth\/login/);
});
