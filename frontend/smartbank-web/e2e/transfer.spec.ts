import { test, expect } from '@playwright/test';
import { DashboardPage, TransferPage, loginAsAlex, getTransferPair } from './poms/pages';

function parseBalance(text: string): number {
  const n = text.replace(/[^0-9.,-]/g, '').replace(/,/g, '');
  return Number(n);
}

test.describe.serial('transfers', () => {
  test('golden journey: internal transfer updates balance', async ({ page, request }) => {
    await loginAsAlex(page, request);
    const dashboard = new DashboardPage(page);
    await dashboard.expectLoaded();

    const transfer = new TransferPage(page);
    await dashboard.quickTransfer().click();
    await expect(page).toHaveURL(/transfers/);
    // Don't rely on the default source: empty E2E-* accounts from other
    // suites may sort first, and the API rejects cross-currency bookings.
    const { destIban, sourceId, sourceIndex, sourceBalance: b0 } = await getTransferPair(request, page, 100);
    await transfer.selectSourceById(sourceId);
    await transfer.fillInternal(destIban, '100.00');
    await expect(transfer.verified()).toBeVisible();
    await transfer.submitAndConfirm();
    await expect(transfer.success()).toBeVisible();
    await expect(transfer.reference()).toHaveText(/^TRN-/);

    await page.goto('/dashboard');
    await dashboard.expectLoaded();
    const b1 = parseBalance(await dashboard.balanceText(sourceIndex));
    expect(b1).toBeCloseTo(b0 - 100, 2);
  });

  test('amount greater than balance disables submit', async ({ page, request }) => {
    await loginAsAlex(page, request);
    const transfer = new TransferPage(page);
    await transfer.goto();
    await transfer.amount().fill('9999999.00');
    await expect(transfer.submit()).toBeDisabled();
  });

  test('invalid iban checksum shows iban-invalid-msg and disables submit', async ({ page, request }) => {
    await loginAsAlex(page, request);
    const transfer = new TransferPage(page);
    await transfer.goto();
    await transfer.iban().fill('DE89370400440532013001');
    await expect(page.getByTestId('iban-invalid-msg')).toBeVisible();
    await expect(transfer.submit()).toBeDisabled();
  });
});
