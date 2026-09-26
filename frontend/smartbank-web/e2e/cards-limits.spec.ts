import { test, expect } from '@playwright/test';
import { CardsPage, loginAsAlex } from './poms/pages';

test('G1 back button returns from detail to list', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/cards');
  const cards = new CardsPage(page);
  await cards.openFirst();
  await cards.back().click();
  await expect(cards.list().first()).toBeVisible();
});

test('G2 limits save disabled when pristine', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/cards');
  const cards = new CardsPage(page);
  await cards.openFirst();
  await expect(cards.limitSave()).toBeDisabled();
  await expect(cards.limitSave()).toContainText(/Saved/);
});

test('G3 moving ecom slider enables save', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/cards');
  const cards = new CardsPage(page);
  await cards.openFirst();
  const slider = cards.ecomSlider();
  const current = Number(await slider.inputValue());
  const next = current >= 10000 ? current - 500 : current + 500;
  await slider.fill(String(next));
  await expect(cards.limitSave()).toBeEnabled();
  await expect(cards.limitSave()).toContainText(/Save limits/);
});

test('G4 detail shows expiry, masked PAN, status and visual', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/cards');
  const cards = new CardsPage(page);
  await cards.openFirst();
  await expect(cards.detail()).toBeVisible();
  await expect(cards.expiry()).toHaveText(/^\d{1,2}\/\d{2}$/);
  await expect(cards.maskedPan()).not.toBeEmpty();
  await expect(cards.status().first()).toHaveText(/Active|Frozen/);
});
