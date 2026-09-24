import '@angular/compiler';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { AccountDetailPage } from './account-detail.page';
import { LedgerService } from './data-access/ledger.service';
import { AccountSelectionStore } from './account-selection.store';
import type { RecentTx } from './data-access/ledger.models';

const tx = (id: string): RecentTx => ({
  transactionId: id,
  reference: `REF-${id}`,
  bookedAt: '2026-01-01T00:00:00Z',
  direction: 'Debit',
  kind: 'Transfer',
  amount: '10.00',
  currency: 'EUR',
  balanceAfter: '990.00',
});

function makePage(opts?: {
  selectedId?: string | null;
  pages?: Record<number, { items: RecentTx[]; total: number }>;
}) {
  const selected = signal<string | null>(opts?.selectedId === undefined ? 'acc-1' : opts.selectedId);
  const selection = { selectedId: selected };
  const getTransactions = vi.fn((id: string, f: { page: number }) => {
    const p = opts?.pages?.[f.page] ?? { items: [], total: 0 };
    void id;
    return of(p);
  });
  const statementCsvUrl = vi.fn(() => 'http://x/statement.csv?format=csv');
  const ledger = { getTransactions, statementCsvUrl };
  const router = { navigate: vi.fn(() => Promise.resolve(true)) };
  const injector = Injector.create([
    { provide: LedgerService, useValue: ledger },
    { provide: Router, useValue: router },
    { provide: AccountSelectionStore, useValue: selection },
  ]);
  const page = runInInjectionContext(injector, () => new AccountDetailPage());
  return { page, ledger, router, getTransactions, statementCsvUrl };
}

beforeEach(() => {
  vi.stubGlobal('window', { open: vi.fn() });
});

describe('AccountDetailPage init', () => {
  it('redirects to /accounts when nothing selected and skips load', () => {
    const { page, router, getTransactions } = makePage({ selectedId: null });
    page.ngOnInit();
    expect(router.navigate).toHaveBeenCalledWith(['/accounts'], { replaceUrl: true });
    expect(getTransactions).not.toHaveBeenCalled();
  });

  it('sets id and loads page 1 when selected', async () => {
    const { page, getTransactions } = makePage({
      pages: { 1: { items: [tx('1')], total: 1 } },
    });
    page.ngOnInit();
    // ngOnInit calls async load() without awaiting
    await vi.waitFor(() => expect(getTransactions).toHaveBeenCalledTimes(1));
    expect(page.id()).toBe('acc-1');
    expect(page.items()).toHaveLength(1);
    expect(page.total()).toBe(1);
    expect(page.hasMore()).toBe(false);
  });
});

describe('AccountDetailPage load', () => {
  it('resets to page 1, forwards filters, toggles loading', async () => {
    const { page, getTransactions } = makePage({
      pages: { 1: { items: [tx('1'), tx('2')], total: 2 } },
    });
    page.id.set('acc-1');
    page.page.set(3);
    page.type.set('Debit');
    page.kind.set('Transfer');
    page.from.set('2026-01-01');
    page.to.set('2026-02-01');
    const p = page.load();
    expect(page.loading()).toBe(true);
    await p;
    expect(page.loading()).toBe(false);
    expect(page.page()).toBe(1);
    expect(getTransactions).toHaveBeenCalledWith(
      'acc-1',
      expect.objectContaining({ page: 1, pageSize: 20, type: 'Debit', kind: 'Transfer' })
    );
    expect(page.hasMore()).toBe(false);
  });

  it('missing payload fields default to empty list / zero total', async () => {
    const { page } = makePage({
      pages: { 1: { items: undefined as never, total: undefined as never } },
    });
    page.id.set('acc-1');
    await page.load();
    expect(page.items()).toEqual([]);
    expect(page.total()).toBe(0);
    expect(page.hasMore()).toBe(false);
  });
});

describe('AccountDetailPage loadMore', () => {
  it('appends next page and advances cursor', async () => {
    const { page } = makePage({
      pages: {
        1: { items: [tx('1')], total: 3 },
        2: { items: [tx('2'), tx('3')], total: 3 },
      },
    });
    page.id.set('acc-1');
    await page.load();
    expect(page.hasMore()).toBe(true);
    await page.loadMore();
    expect(page.page()).toBe(2);
    expect(page.items().map((t) => t.transactionId)).toEqual(['1', '2', '3']);
    expect(page.hasMore()).toBe(false);
    expect(page.loadingMore()).toBe(false);
  });

  it('no-ops when already loading or nothing left', async () => {
    const { page, getTransactions } = makePage({
      pages: { 1: { items: [tx('1')], total: 1 } },
    });
    page.id.set('acc-1');
    await page.load();
    const calls = getTransactions.mock.calls.length;
    page.loadingMore.set(true);
    await page.loadMore();
    expect(getTransactions.mock.calls.length).toBe(calls);
    page.loadingMore.set(false);
    await page.loadMore(); // hasMore false
    expect(getTransactions.mock.calls.length).toBe(calls);
  });
});

describe('AccountDetailPage exportCsv', () => {
  it('opens the csv url in a new tab', () => {
    const { page, statementCsvUrl } = makePage();
    page.id.set('acc-7');
    page.from.set('2026-01-01');
    page.to.set('2026-03-01');
    page.exportCsv();
    expect(statementCsvUrl).toHaveBeenCalledWith('acc-7', '2026-01-01', '2026-03-01');
    expect(window.open).toHaveBeenCalledWith('http://x/statement.csv?format=csv', '_blank');
  });
});
