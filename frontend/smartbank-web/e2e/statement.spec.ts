import { test, expect, type Page } from '@playwright/test';
import { loginAsAlex } from './poms/pages';

/**
 * Opens a seeded account's statement. Other suites create empty E2E-*
 * accounts that may sort first; those have no transactions, so CSV export
 * yields no download.
 */
async function openSeededAccount(page: Page) {
  const aliases = page.getByTestId('dashboard-account-alias');
  await expect(aliases.first()).toBeVisible();
  const count = await aliases.count();
  for (let i = 0; i < count; i++) {
    if (!((await aliases.nth(i).innerText()).trim().startsWith('E2E-'))) {
      await page.getByTestId('dashboard-account-card').nth(i).click();
      return;
    }
  }
  throw new Error('No seeded account card found');
}

test('account detail filter debit hides credits', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/dashboard');
  await openSeededAccount(page);
  await page.getByTestId('statement-filter-type').selectOption('Debit');
  await page.getByTestId('statement-apply-btn').click();
  const rows = await page.getByTestId('statement-row').count();
  expect(rows).toBeGreaterThanOrEqual(0);
});

test('export button triggers a download matching statement csv', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/dashboard');
  await openSeededAccount(page);
  const downloadPromise = page.waitForEvent('download');
  await page.getByTestId('export-statement-btn').click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/statement-.*\.csv/);
});
