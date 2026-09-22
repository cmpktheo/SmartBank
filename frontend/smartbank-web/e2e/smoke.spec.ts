import { test, expect } from '@playwright/test';

test('smoke: has SmartBank title', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/SmartBank/);
});
