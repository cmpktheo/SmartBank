import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { compactIban } from '../../../core/iban';

export type IbanStatus = 'unknown' | 'valid' | 'invalid' | 'verified' | 'unverified';

export interface IbanVerification {
  status: IbanStatus;
  holderName: string;
}

export interface BookTransferPayload {
  sourceAccountId: string;
  destinationIban: string;
  amount: string;
  currency: string;
  transferType: string;
  narrative?: string;
}

@Injectable({ providedIn: 'root' })
export class TransfersService {
  private http = inject(HttpClient);

  verifyIban(raw: string): Observable<{ verified: boolean; accountHolderName?: string }> {
    return this.http.get<{ verified: boolean; accountHolderName?: string }>(
      `${environment.apiBaseUrl}/api/accounts/iban/${compactIban(raw)}`
    );
  }

  bookTransfer(payload: BookTransferPayload): Observable<{ reference: string }> {
    return this.http.post<{ reference: string }>(
      `${environment.apiBaseUrl}/api/transfers`,
      {
        ...payload,
        destinationIban: compactIban(payload.destinationIban),
        narrative: payload.narrative || undefined,
      },
      { headers: { 'Idempotency-Key': crypto.randomUUID() } }
    );
  }
}
