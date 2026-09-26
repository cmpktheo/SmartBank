import { test, expect } from '@playwright/test';
import { AccountListPage, loginAsAlex } from './poms/pages';

test('D1 accounts page shows total count and grid', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/accounts');
  await expect(page.getByText(/total/)).toBeVisible();
  await expect(page.getByTestId('dashboard-account-card').first()).toBeVisible();
});

test('D2 Open new account toggle reveals form', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/accounts');
  await page.getByRole('button', { name: /open new account/i }).click();
  await expect(page.getByTestId('account-new-alias')).toBeVisible();
  await expect(page.getByTestId('account-new-submit')).toBeVisible();
});

test('D3 empty alias keeps submit disabled', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/accounts');
  await page.getByRole('button', { name: /open new account/i }).click();
  await page.getByTestId('account-new-alias').fill('');
  await expect(page.getByTestId('account-new-submit')).toBeDisabled();
});

test('D4 creating an account appends a card with that alias', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/accounts');
  const list = new AccountListPage(page);
  await list.expectLoaded();
  const before = await list.accountCount();
  const alias = `E2E-${Date.now()}`;
  await page.getByRole('button', { name: /open new account/i }).click();
  await list.aliasInput().fill(alias);
  await list.submitBtn().click();
  await expect(page.getByTestId('dashboard-account-alias').filter({ hasText: alias })).toBeVisible();
  expect(await list.accountCount()).toBeGreaterThanOrEqual(before + 1);
});
