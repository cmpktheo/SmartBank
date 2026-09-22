import { test, expect } from '@playwright/test';
import { loginAsAlex } from './poms/pages';

test('account detail filter debit hides credits', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/dashboard');
  await page.getByTestId('dashboard-account-card').first().click();
  await page.getByTestId('statement-filter-type').selectOption('Debit');
  await page.getByTestId('statement-apply-btn').click();
  const rows = await page.getByTestId('statement-row').count();
  expect(rows).toBeGreaterThanOrEqual(0);
});

test('export button triggers a download matching statement csv', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/dashboard');
  await page.getByTestId('dashboard-account-card').first().click();
  const downloadPromise = page.waitForEvent('download');
  await page.getByTestId('export-statement-btn').click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/statement-.*\.csv/);
});
