import '@angular/compiler';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { TransferPage } from './transfer.page';
import { AccountsService } from '../accounts/data-access/accounts.service';
import { TransfersService } from './data-access/transfers.service';
import type { AccountSummary } from '../../core/models/models';

const acc1: AccountSummary = {
  id: 'acc-1',
  alias: 'Everyday',
  iban: 'DE89370400440532013000',
  ibanFormatted: 'DE89 3704 0044 0532 0130 00',
  type: 'Current',
  status: 'Active',
  currency: 'EUR',
  availableBalance: '1000.00',
  postedBalance: '1000.00',
};

const acc2: AccountSummary = {
  id: 'acc-2',
  alias: 'Savings',
  iban: 'GB29NWBK60161331926819',
  ibanFormatted: 'GB29 NWBK 6016 1331 9268 19',
  type: 'Savings',
  status: 'Active',
  currency: 'EUR',
  availableBalance: '50.00',
  postedBalance: '50.00',
};

function makePage(opts?: {
  accounts?: AccountSummary[];
  verifyIban?: (raw: string) => unknown;
  bookTransfer?: (payload: unknown) => unknown;
}) {
  const accountsService = {
    getAccounts: () => of(opts?.accounts ?? [acc1, acc2]),
  };
  const transfersService = {
    verifyIban: (raw: string) =>
      (opts?.verifyIban ? opts.verifyIban(raw) : of({ verified: false })) as never,
    bookTransfer: (payload: unknown) =>
      (opts?.bookTransfer ? opts.bookTransfer(payload) : of({ reference: 'TRN-1' })) as never,
  };
  const injector = Injector.create([
    { provide: AccountsService, useValue: accountsService },
    { provide: TransfersService, useValue: transfersService },
  ]);
  const page = runInInjectionContext(injector, () => new TransferPage());
  return { page, accountsService, transfersService };
}

beforeEach(() => {
  vi.stubGlobal('document', { querySelector: () => null });
  vi.useRealTimers();
});

describe('TransferPage computed state', () => {
  it('ngOnInit loads accounts and selects the first', async () => {
    const { page } = makePage();
    await page.ngOnInit();
    expect(page.accounts()).toHaveLength(2);
    expect(page.selectedSourceId()).toBe('acc-1');
    expect(page.sourceAlias()).toBe('Everyday');
    expect(page.sourceOptions()).toHaveLength(2);
    expect(page.sourceOptions()[0].value).toBe('acc-1');
  });

  it('insufficient is true only when amount exceeds balance', async () => {
    const { page } = makePage();
    await page.ngOnInit();
    page.amount.set('');
    expect(page.insufficient()).toBe(false);
    page.amount.set('100.00');
    expect(page.insufficient()).toBe(false);
    page.amount.set('1000.01');
    expect(page.insufficient()).toBe(true);
  });

  it.each([
    ['no source', { source: '', amount: '10', iban: 'verified' as const, submitting: false, expected: true }],
    ['no amount', { source: 'acc-1', amount: '', iban: 'verified' as const, submitting: false, expected: true }],
    ['zero amount', { source: 'acc-1', amount: '0', iban: 'verified' as const, submitting: false, expected: true }],
    ['negative amount', { source: 'acc-1', amount: '-5', iban: 'verified' as const, submitting: false, expected: true }],
    ['invalid iban', { source: 'acc-1', amount: '10', iban: 'invalid' as const, submitting: false, expected: true }],
    ['insufficient funds', { source: 'acc-2', amount: '999', iban: 'verified' as const, submitting: false, expected: true }],
    ['submitting', { source: 'acc-1', amount: '10', iban: 'verified' as const, submitting: true, expected: true }],
    ['happy path verified', { source: 'acc-1', amount: '10', iban: 'verified' as const, submitting: false, expected: false }],
    ['happy path unverified still submittable', { source: 'acc-1', amount: '10', iban: 'unverified' as const, submitting: false, expected: false }],
  ])('submitDisabled: %s', async (_name, c) => {
    const { page } = makePage();
    await page.ngOnInit();
    page.selectedSourceId.set(c.source);
    page.amount.set(c.amount);
    page.ibanStatus.set(c.iban);
    page.submitting.set(c.submitting);
    expect(page.submitDisabled()).toBe(c.expected);
  });
});

describe('TransferPage verifyIban', () => {
  it('empty iban -> unknown', async () => {
    const { page } = makePage();
    page.iban.set('');
    await page.verifyIban();
    expect(page.ibanStatus()).toBe('unknown');
  });

  it('bad checksum -> invalid without backend call', async () => {
    const verifyIban = vi.fn(() => of({ verified: true }));
    const { page } = makePage({ verifyIban });
    page.iban.set('DE89370400440532013001');
    await page.verifyIban();
    expect(page.ibanStatus()).toBe('invalid');
    expect(verifyIban).not.toHaveBeenCalled();
  });

  it('verified iban sets verified + holder name', async () => {
    const { page } = makePage({
      verifyIban: () => of({ verified: true, accountHolderName: 'Jordan Lee' }),
    });
    page.iban.set('DE89370400440532013000');
    await page.verifyIban();
    expect(page.ibanStatus()).toBe('verified');
    expect(page.holderName()).toBe('Jordan Lee');
  });

  it('unverified iban sets unverified', async () => {
    const { page } = makePage({ verifyIban: () => of({ verified: false }) });
    page.iban.set('DE89370400440532013000');
    await page.verifyIban();
    expect(page.ibanStatus()).toBe('unverified');
  });

  it('backend error falls back to valid (checksum ok, verification unknown)', async () => {
    const { page } = makePage({ verifyIban: () => throwError(() => new Error('down')) });
    page.iban.set('DE89370400440532013000');
    await page.verifyIban();
    expect(page.ibanStatus()).toBe('valid');
  });
});

describe('TransferPage confirm flow', () => {
  async function readyPage(overrides?: Parameters<typeof makePage>[0]) {
    const ctx = makePage(overrides);
    await ctx.page.ngOnInit();
    ctx.page.selectedSourceId.set('acc-1');
    ctx.page.amount.set('10.00');
    ctx.page.iban.set('DE89370400440532013000');
    ctx.page.ibanStatus.set('verified');
    return ctx;
  }

  it('openConfirm does nothing when disabled', async () => {
    const { page } = await readyPage();
    page.amount.set('');
    expect(page.submitDisabled()).toBe(true);
    page.openConfirm();
    expect(page.confirmOpen()).toBe(false);
  });

  it('openConfirm opens modal and clears apiError', async () => {
    const { page } = await readyPage();
    page.apiError.set('old');
    page.openConfirm();
    expect(page.confirmOpen()).toBe(true);
    expect(page.apiError()).toBeNull();
  });

  it('confirm success sets reference and closes modal', async () => {
    const { page } = await readyPage({ bookTransfer: () => of({ reference: 'TRN-ABC' }) });
    page.confirmOpen.set(true);
    await page.confirm();
    expect(page.successReference()).toBe('TRN-ABC');
    expect(page.confirmOpen()).toBe(false);
    expect(page.submitting()).toBe(false);
    expect(page.apiError()).toBeNull();
  });

  it('confirm failure surfaces backend detail and stops submitting', async () => {
    const { page } = await readyPage({
      bookTransfer: () => throwError(() => ({ error: { detail: 'Insufficient funds.' } })),
    });
    page.confirmOpen.set(true);
    await page.confirm();
    expect(page.apiError()).toBe('Insufficient funds.');
    expect(page.submitting()).toBe(false);
    expect(page.successReference()).toBe('');
  });
});
