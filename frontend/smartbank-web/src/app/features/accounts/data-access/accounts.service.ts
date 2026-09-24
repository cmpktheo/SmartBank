import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { AccountSummary } from '../../../core/models/models';

@Injectable({ providedIn: 'root' })
export class AccountsService {
  private http = inject(HttpClient);

  getAccounts(): Observable<AccountSummary[]> {
    return this.http.get<AccountSummary[]>(`${environment.apiBaseUrl}/api/accounts`);
  }

  openAccount(alias: string, type: string, currency: string): Observable<AccountSummary> {
    return this.http.post<AccountSummary>(`${environment.apiBaseUrl}/api/accounts`, {
      alias,
      type,
      currency,
    });
  }
}
