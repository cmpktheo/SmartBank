import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { AccountSummary } from '../../../core/models/models';
import type { CurrencyTotal, RecentTx } from './ledger.models';

export interface StatementFilters {
  page: number;
  pageSize: number;
  type: string;
  kind: string;
  from: string;
  to: string;
}

export interface StatementPage {
  items: RecentTx[];
  total: number;
}

@Injectable({ providedIn: 'root' })
export class LedgerService {
  private http = inject(HttpClient);

  getRecent(accountId: string, limit = 10): Observable<RecentTx[]> {
    return this.http.get<RecentTx[]>(
      `${environment.apiBaseUrl}/api/ledger/accounts/${accountId}/recent?limit=${limit}`
    );
  }

  getTransactions(accountId: string, filters: StatementFilters): Observable<StatementPage> {
    const params = new URLSearchParams({
      page: String(filters.page),
      pageSize: String(filters.pageSize),
      type: filters.type,
      kind: filters.kind,
    });
    if (filters.from) params.set('from', new Date(filters.from).toISOString());
    if (filters.to) params.set('to', new Date(filters.to).toISOString());
    return this.http.get<StatementPage>(
      `${environment.apiBaseUrl}/api/ledger/accounts/${accountId}/transactions?${params}`
    );
  }

  statementCsvUrl(accountId: string, from: string, to: string): string {
    const params = new URLSearchParams({ format: 'csv' });
    if (from) params.set('from', new Date(from).toISOString());
    if (to) params.set('to', new Date(to).toISOString());
    return `${environment.apiBaseUrl}/api/ledger/accounts/${accountId}/statement.csv?${params}`;
  }

  /** CSV content via authenticated XHR (auth interceptor attaches Bearer).
   *  A plain window.open() can't send the header, so it 401s. */
  getStatementCsv(accountId: string, from: string, to: string): Observable<string> {
    return this.http.get(this.statementCsvUrl(accountId, from, to), { responseType: 'text' });
  }

  /** Prefer the Current account, fall back to the first one. Shared by dashboard + detail. */
  preferCurrentAccount(accounts: AccountSummary[]): AccountSummary | undefined {
    return accounts.find((a) => a.type === 'Current') ?? accounts[0];
  }

  /** Group available balances per currency. No FX conversion is applied.
   *  Accounts with blank/non-numeric balances are skipped so one corrupt
   *  value can't poison the whole bucket (Number('n/a') is NaN, and NaN
   *  propagates through addition). */
  totalsByCurrency(accounts: AccountSummary[]): CurrencyTotal[] {
    const map = new Map<string, number>();
    for (const a of accounts) {
      const n = Number(a.availableBalance);
      if (!Number.isFinite(n)) continue;
      map.set(a.currency, (map.get(a.currency) ?? 0) + n);
    }
    return [...map.entries()].map(([currency, total]) => ({ currency, total: String(total) }));
  }
}
