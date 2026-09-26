import { test, expect, type Page } from '@playwright/test';
import { AccountDetailPage, loginAsAlex } from './poms/pages';

async function openDetail(page: Page) {
  await page.goto('/dashboard');
  await page.getByTestId('dashboard-account-card').first().click();
  await expect(page).toHaveURL(/accounts\/detail/);
}

test('E1 direct detail without selection redirects to /accounts', async ({ page, request }) => {
  await loginAsAlex(page, request);
  // Clear selected account so guard logic kicks in
  await page.evaluate(() => sessionStorage.removeItem('sb.selectedAccountId'));
  await page.goto('/accounts/detail');
  // Exact /accounts match: a bare /accounts/ pattern also matches /accounts/detail.
  await expect(page).toHaveURL(/\/accounts\/?(\?.*)?$/);
});

test('E2 kind filter CardPayment keeps valid table state', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await openDetail(page);
  const detail = new AccountDetailPage(page);
  await detail.kindSelect().selectOption('CardPayment');
  await detail.applyBtn().click();
  await expect(page.getByText(/Showing \d+ of \d+ transactions/)).toBeVisible();
});

test('E3 date from/to filter keeps valid table state', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await openDetail(page);
  const detail = new AccountDetailPage(page);
  await detail.fromInput().fill('2024-01-01');
  await detail.toInput().fill('2026-12-31');
  await detail.applyBtn().click();
  await expect(page.getByText(/Showing \d+ of \d+ transactions/)).toBeVisible();
});

test('E4 footer count visible, Load more only when hasMore', async ({ page, request }) => {
  await loginAsAlex(page, request);
  await openDetail(page);
  await expect(page.getByText(/Showing \d+ of \d+ transactions/)).toBeVisible();
  const loadMore = page.getByTestId('statement-load-more-btn');
  const rows = await page.getByTestId('statement-row').count();
  const footer = (await page.getByText(/Showing \d+ of \d+ transactions/).innerText()).trim();
  const m = footer.match(/Showing (\d+) of (\d+)/);
  if (m && Number(m[1]) < Number(m[2])) {
    await expect(loadMore).toBeVisible();
  } else {
    expect(rows).toBeLessThanOrEqual(20);
  }
});
