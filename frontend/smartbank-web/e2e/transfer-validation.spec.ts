import { test, expect } from '@playwright/test';
import { TransferPage, loginAsAlex, getInternalDestinationIban, getTransferPair } from './poms/pages';

test('F1 empty amount disables Review', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const t = new TransferPage(page);
  await t.goto();
  await t.amount().fill('');
  await expect(t.submit()).toBeDisabled();
});

test('F2 Review opens modal, Cancel closes without success', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const t = new TransferPage(page);
  await t.goto();
  const { destIban, sourceId } = await getTransferPair(request, page, 10);
  await t.selectSourceById(sourceId);
  await t.fillInternal(destIban, '10.00');
  await expect(t.verified()).toBeVisible();
  await t.submit().click();
  await expect(page.getByTestId('transfer-confirm-modal')).toBeVisible();
  await page.getByTestId('transfer-cancel-btn').click();
  await expect(page.getByTestId('transfer-confirm-modal')).toBeHidden();
  await expect(t.success()).toBeHidden();
});

test('F3 non-Internal type shows Ledger warning', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const t = new TransferPage(page);
  await t.goto();
  await page.getByTestId('transfer-type-domestic').click();
  await expect(page.getByText(/only settles.*Internal/)).toBeVisible();
});

test('F4 insufficient amount shows message and disables Review', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const t = new TransferPage(page);
  await t.goto();
  const dest = await getInternalDestinationIban(request, page);
  await t.iban().fill(dest);
  await t.amount().fill('9999999.00');
  await expect(page.getByTestId('transfer-insufficient-msg')).toBeVisible();
  await expect(t.submit()).toBeDisabled();
});

test('F5 narrative accepted, confirm dialog echoes amount', async ({ page, request }) => {
  await loginAsAlex(page, request);
  const t = new TransferPage(page);
  await t.goto();
  const { destIban, sourceId } = await getTransferPair(request, page, 5);
  await t.selectSourceById(sourceId);
  await t.fillInternal(destIban, '5.00');
  await page.getByTestId('transfer-narrative-input').fill('Invoice #4922');
  await expect(t.verified()).toBeVisible();
  await t.submit().click();
  await expect(page.getByTestId('transfer-confirm-amount')).toContainText('5.00');
});
