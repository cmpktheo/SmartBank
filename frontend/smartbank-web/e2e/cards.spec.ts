import { test, expect } from '@playwright/test';
import { CardsPage, loginAsAlex } from './poms/pages';

test('cards list shows masked PAN with chevron and opens detail', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/cards');
  const cards = new CardsPage(page);
  await expect(cards.list().first()).toBeVisible();
  await expect(cards.list().first().getByTestId('card-list-pan')).toContainText(/•/);
  await cards.list().first().click();
  await expect(cards.freeze().first()).toBeVisible();
  await expect(page.getByTestId('card-back-btn')).toBeVisible();
});

test('freeze toggle sets status to Frozen', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/cards');
  const cards = new CardsPage(page);
  await cards.openFirst();
  await cards.freeze().first().check();
  await expect(cards.status().first()).toHaveText(/Frozen/);
});

test('unfreeze sets Active', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/cards');
  const cards = new CardsPage(page);
  await cards.openFirst();
  await cards.freeze().first().uncheck();
  await expect(cards.status().first()).toHaveText(/Active/);
});

test('cvv toggle reveals 3 digits', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await page.goto('/cards');
  const cards = new CardsPage(page);
  await cards.openFirst();
  await cards.cvvToggle().first().click();
  await expect(cards.cvv().first()).toHaveText(/^[0-9]{3}$/);
});
