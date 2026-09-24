import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { CardDto } from '../../../core/models/models';

@Injectable({ providedIn: 'root' })
export class CardsService {
  private http = inject(HttpClient);

  getCards(): Observable<CardDto[]> {
    return this.http.get<CardDto[]>(`${environment.apiBaseUrl}/api/cards`);
  }

  freeze(cardId: string): Observable<CardDto> {
    return this.http.post<CardDto>(
      `${environment.apiBaseUrl}/api/cards/${cardId}/freeze`,
      {},
      { headers: { 'Idempotency-Key': crypto.randomUUID() } }
    );
  }

  unfreeze(cardId: string): Observable<CardDto> {
    return this.http.post<CardDto>(
      `${environment.apiBaseUrl}/api/cards/${cardId}/unfreeze`,
      {},
      { headers: { 'Idempotency-Key': crypto.randomUUID() } }
    );
  }

  revealCvv(cardId: string): Observable<{ cvv: string }> {
    return this.http.post<{ cvv: string }>(
      `${environment.apiBaseUrl}/api/cards/${cardId}/reveal-cvv`,
      {}
    );
  }

  saveLimits(cardId: string, ecom: number, atm: number): Observable<CardDto> {
    return this.http.patch<CardDto>(`${environment.apiBaseUrl}/api/cards/${cardId}/limits`, {
      dailyEcommerceLimit: String(ecom),
      dailyAtmLimit: String(atm),
    });
  }
}
