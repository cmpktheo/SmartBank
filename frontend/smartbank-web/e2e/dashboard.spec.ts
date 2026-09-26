import { test, expect } from '@playwright/test';
import { DashboardPage, loginAsAlex } from './poms/pages';

test('C1 totals row shows per-currency total', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await expect(page.getByText(/Total in (EUR|USD|GBP)/).first()).toBeVisible();
});

test('C2 account cards expose alias, IBAN and balance', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const dash = new DashboardPage(page);
  await dash.expectLoaded();
  await expect(page.getByTestId('dashboard-account-alias').first()).not.toBeEmpty();
  await expect(page.getByTestId('dashboard-account-iban').first()).not.toBeEmpty();
  await expect(page.getByTestId('dashboard-account-balance').first()).not.toBeEmpty();
});

test('C3 quick Manage cards navigates to /cards', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.getByTestId('quick-cards-btn').click();
  await expect(page).toHaveURL(/cards/);
});

test('C4 clicking dashboard account card opens statement detail', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const dash = new DashboardPage(page);
  await dash.expectLoaded();
  await dash.accountCards().first().click();
  await expect(page).toHaveURL(/accounts\/detail/);
});

test('C5 recent transactions render or show empty state', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const dash = new DashboardPage(page);
  await dash.expectLoaded();
  const count = await dash.recent().count();
  if (count > 0) {
    await expect(page.getByTestId('recent-transaction-amount').first()).toBeVisible();
  } else {
    await expect(page.getByText(/No recent activity/)).toBeVisible();
  }
});
