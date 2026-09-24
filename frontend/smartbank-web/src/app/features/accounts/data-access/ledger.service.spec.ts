import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { LedgerService } from './ledger.service';
import type { AccountSummary } from '../../../core/models/models';

function makeService() {
  const injector = Injector.create([{ provide: HttpClient, useValue: {} }]);
  return runInInjectionContext(injector, () => new LedgerService());
}

const current: AccountSummary = {
  id: 'c', alias: 'Everyday', iban: 'x', ibanFormatted: 'x',
  type: 'Current', status: 'Active', currency: 'EUR',
  availableBalance: '100.00', postedBalance: '100.00',
};
const savings: AccountSummary = {
  id: 's', alias: 'Savings', iban: 'x', ibanFormatted: 'x',
  type: 'Savings', status: 'Active', currency: 'EUR',
  availableBalance: '50.00', postedBalance: '50.00',
};

describe('LedgerService pure helpers', () => {
  it('preferCurrentAccount picks Current, falls back to first, undefined when empty', () => {
    const svc = makeService();
    expect(svc.preferCurrentAccount([savings, current])?.id).toBe('c');
    expect(svc.preferCurrentAccount([savings])?.id).toBe('s');
    expect(svc.preferCurrentAccount([])).toBeUndefined();
  });

  it('totalsByCurrency groups without FX conversion', () => {
    const svc = makeService();
    const usd: AccountSummary = { ...savings, id: 'u', currency: 'USD', availableBalance: '20.00' };
    expect(svc.totalsByCurrency([current, savings, usd])).toEqual([
      { currency: 'EUR', total: '150' },
      { currency: 'USD', total: '20' },
    ]);
    expect(svc.totalsByCurrency([])).toEqual([]);
  });

  it('skips corrupt balances instead of poisoning the currency total', () => {
    const svc = makeService();
    const corrupt: AccountSummary = { ...savings, id: 'bad', availableBalance: 'n/a' };
    expect(svc.totalsByCurrency([current, corrupt])).toEqual([{ currency: 'EUR', total: '100' }]);
    expect(svc.totalsByCurrency([corrupt])).toEqual([]);
  });

  it('empty-string balance counts as 0', () => {
    const svc = makeService();
    const empty: AccountSummary = { ...savings, id: 'empty', availableBalance: '' };
    expect(svc.totalsByCurrency([current, empty])).toEqual([{ currency: 'EUR', total: '100' }]);
  });

  it('statementCsvUrl includes ISO dates only when set', () => {
    const svc = makeService();
    const bare = svc.statementCsvUrl('acc-1', '', '');
    expect(bare).toContain('/api/ledger/accounts/acc-1/statement.csv');
    expect(bare).toContain('format=csv');
    expect(bare).not.toContain('from=');

    const ranged = svc.statementCsvUrl('acc-1', '2026-01-01', '2026-02-01');
    expect(ranged).toContain('from=2026-01-01');
    expect(ranged).toContain('to=2026-02-01');
  });
});
