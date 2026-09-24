import { Component, computed, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { LucideArrowLeft, LucideSnowflake, LucideChevronRight } from '@lucide/angular';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { CardDto } from '../../core/models/models';

@Component({
  selector: 'sb-cards',
  standalone: true,
  imports: [FormsModule, LucideArrowLeft, LucideSnowflake, LucideChevronRight],
  template: `
    <div class="sb-container tab-pane">
      <div><h2 style="font-size:22px;color:#fff">Cards</h2>
        <p class="muted" style="font-size:12.5px">Freeze / limits / CVV reveal — card issuance is automatic, full PAN is never exposed</p></div>
      @if (loading()) { <p class="muted">Loading cards…</p> }
      @else {
        @if (selectedCard(); as card) {
        <button data-testid="card-back-btn" class="btn-secondary" style="align-self:flex-start;font-size:12.5px;padding:7px 12px" (click)="back()"><svg lucideArrowLeft style="width:14px;height:14px;vertical-align:-2px" /> All cards</button>
        <div class="card card-pad" data-testid="card-detail" style="max-width:560px">
          <div class="bank-card" data-testid="card-visual" style="margin:0 auto 16px">
              <div class="row-between"><span style="font-weight:700;letter-spacing:.06em;font-size:13px;font-family:Outfit,sans-serif">SMARTBANK</span>
                <span style="display:flex;align-items:center;gap:10px"><img src="assets/cards/contactless.svg" alt="" aria-hidden="true" width="22" height="22" style="display:block;opacity:.9" />
                  @if (isVisa(card)) {
                    <img data-testid="card-brand-visa" src="assets/cards/visa.svg" alt="Visa" title="Visa" width="42" height="26" style="display:block;border-radius:4px" />
                  } @else if (isMastercard(card)) {
                    <img data-testid="card-brand-mastercard" src="assets/cards/mastercard.svg" alt="Mastercard" title="Mastercard" width="42" height="26" style="display:block;border-radius:4px" />
                  }
                </span></div>
              <div data-testid="card-masked-pan" class="mono tnum" style="font-size:19px;letter-spacing:.1em">{{ card.maskedPan }}</div>
              <div class="row-between" style="font-size:12px">
                <div><span class="muted" style="font-size:9px;display:block;letter-spacing:.15em">ACCOUNT</span><span class="mono" style="font-size:11px">{{ card.accountId.slice(0,8) }}…</span></div>
                <div style="text-align:right"><span class="muted" style="font-size:9px;display:block;letter-spacing:.15em">EXPIRES</span>
                  <span data-testid="card-expiry" class="mono tnum">{{ card.expiryMonth }}/{{ card.expiryYear % 100 }}</span></div>
                <div style="text-align:right"><span class="muted" style="font-size:9px;display:block;letter-spacing:.15em">CVV</span>
                  <span data-testid="card-cvv-value" class="mono">{{ cvvFor(card.id) ?? '•••' }}</span></div>
              </div>
              @if (card.status === 'Frozen') {
                <div class="bank-card-frozen"><svg lucideSnowflake style="width:14px;height:14px;vertical-align:-2px" /> FROZEN</div>
              }
            </div>

            <div class="row-between" style="margin-bottom:12px">
              <span data-testid="card-status" class="pill" [class.pill-green]="card.status==='Active'" [class.pill-amber]="card.status==='Frozen'">{{ card.status }}</span>
              <button data-testid="card-cvv-toggle" class="btn-secondary" style="font-size:12px;padding:6px 12px" (click)="reveal(card.id)">Show CVV</button>
            </div>
            <label style="display:flex;align-items:center;justify-content:space-between;font-size:12.5px;color:#cbd5e1;background:rgba(0,0,0,.25);border:1px solid rgba(255,255,255,.07);border-radius:10px;padding:10px 12px;cursor:pointer">
              {{ card.status === 'Frozen' ? 'Unfreeze card' : 'Freeze card' }}
              <input data-testid="card-freeze-toggle" type="checkbox" [checked]="card.status === 'Frozen'" (change)="toggleFreeze(card)" style="width:16px;height:16px;accent-color:#2563eb" />
            </label>

            <div style="margin-top:12px;display:flex;flex-direction:column;gap:10px">
              <div><div class="row-between" style="font-size:12px;margin-bottom:4px"><span class="muted">E-commerce daily limit ({{ card.currency }})</span><strong class="tnum" style="color:#fff">{{ ecomFor(card) }}</strong></div>
                <input data-testid="card-limit-slider" type="range" min="0" max="10000" step="50" [ngModel]="ecomFor(card)" (ngModelChange)="setEcom(card.id, $event)" style="width:100%;accent-color:#2563eb" /></div>
              <div><div class="row-between" style="font-size:12px;margin-bottom:4px"><span class="muted">ATM daily limit ({{ card.currency }})</span><strong class="tnum" style="color:#fff">{{ atmFor(card) }}</strong></div>
                <input data-testid="card-limit-slider-atm" type="range" min="0" max="2000" step="50" [ngModel]="atmFor(card)" (ngModelChange)="setAtm(card.id, $event)" style="width:100%;accent-color:#2563eb" /></div>
              <button data-testid="card-limit-save-btn" class="btn-primary" style="width:100%" [disabled]="!isDirty(card) || isSaving(card.id)" (click)="saveLimits(card.id)">{{
                isSaving(card.id) ? 'Saving…' : isDirty(card) ? 'Save limits (' + ecomFor(card) + ' / ' + atmFor(card) + ')' : 'Saved (' + card.dailyEcommerceLimit + ' / ' + card.dailyAtmLimit + ')'
              }}</button>
              @if (saveErrorFor(card.id)) { <p data-testid="card-limit-error" style="font-size:12px;color:#f87171;margin:0">{{ saveErrorFor(card.id) }}</p> }
            </div>
        </div>
      } @else {
        <div class="card" data-testid="card-list" style="padding:8px;max-width:720px">
          @for (card of cards(); track card.id) {
            <button data-testid="card-list-item" (click)="select(card.id)" class="card-hover"
              style="display:flex;align-items:center;gap:14px;width:100%;text-align:left;background:transparent;border:none;border-radius:12px;padding:12px;cursor:pointer;color:inherit">
              <span aria-hidden="true" style="flex-shrink:0;width:64px;height:42px;border-radius:8px;background:linear-gradient(135deg,#1d4ed8,#1e2a5a 55%,#070b16);border:1px solid rgba(96,165,250,.25);display:flex;align-items:center;justify-content:center;overflow:hidden">
                @if (isVisa(card)) {
                  <img src="assets/cards/visa.svg" alt="" width="30" height="19" style="display:block;border-radius:2px" />
                } @else if (isMastercard(card)) {
                  <img src="assets/cards/mastercard.svg" alt="" width="30" height="19" style="display:block;border-radius:2px" />
                } @else {
                  <span style="font-size:10px;font-weight:700;letter-spacing:.06em;color:#fff">CARD</span>
                }
              </span>
              <span style="flex:1;min-width:0">
                <span data-testid="card-list-pan" class="mono tnum" style="display:block;font-size:14px;letter-spacing:.06em;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{{ card.maskedPan }}</span>
                <span class="muted" style="display:block;font-size:11.5px;margin-top:2px">{{ card.status }} · Exp {{ card.expiryMonth }}/{{ card.expiryYear % 100 }}</span>
              </span>
              <svg lucideChevronRight data-testid="card-list-chevron" aria-hidden="true" style="flex-shrink:0;width:20px;height:20px;color:#64748b" />
            </button>
            @if (!$last) { <div style="height:1px;background:rgba(255,255,255,.06);margin:0 12px"></div> }
          } @empty {
            <p class="muted" style="padding:12px">No cards yet — cards are issued automatically when you open an account.</p>
          }
        </div>
        }
      }
    </div>
  `,
})
export class CardsPage implements OnInit {
  private http = inject(HttpClient);
  cards = signal<CardDto[]>([]);
  cvvs = signal<Record<string, string>>({});
  ecomLimits = signal<Record<string, number>>({});
  atmLimits = signal<Record<string, number>>({});
  saving = signal<Record<string, boolean>>({});
  saveErrors = signal<Record<string, string | null>>({});
  loading = signal(true);
  selectedId = signal<string | null>(null);
  selectedCard = computed(() => this.cards().find((c) => c.id === this.selectedId()) ?? null);

  select(id: string) {
    this.selectedId.set(id);
  }

  back() {
    this.selectedId.set(null);
  }

  ecomFor(card: CardDto): number {
    const draft = this.ecomLimits()[card.id];
    if (draft !== undefined) return draft;
    const parsed = Number(card.dailyEcommerceLimit);
    return Number.isFinite(parsed) ? parsed : 2000;
  }

  atmFor(card: CardDto): number {
    const draft = this.atmLimits()[card.id];
    if (draft !== undefined) return draft;
    const parsed = Number(card.dailyAtmLimit);
    return Number.isFinite(parsed) ? parsed : 500;
  }

  setEcom(id: string, value: number) {
    this.ecomLimits.update((m) => ({ ...m, [id]: Number(value) }));
  }

  setAtm(id: string, value: number) {
    this.atmLimits.update((m) => ({ ...m, [id]: Number(value) }));
  }

  isSaving(id: string): boolean {
    return !!this.saving()[id];
  }

  saveErrorFor(id: string): string | null {
    return this.saveErrors()[id] ?? null;
  }

  isDirty(card: CardDto): boolean {
    return this.ecomFor(card) !== Number(card.dailyEcommerceLimit) || this.atmFor(card) !== Number(card.dailyAtmLimit);
  }

  cvvFor(id: string): string | null {
    return this.cvvs()[id] ?? null;
  }

  isVisa(card: CardDto): boolean {
    const brand = card.brand?.toLowerCase();
    if (brand) return brand === 'visa';
    // Fallback for payloads without brand: all currently issued cards are Visa test PANs (start with 4)
    return true;
  }

  isMastercard(card: CardDto): boolean {
    return card.brand?.toLowerCase() === 'mastercard';
  }

  async ngOnInit() {
    try {
      const cards = await firstValueFrom(this.http.get<CardDto[]>(`${environment.apiBaseUrl}/api/cards`));
      this.cards.set(cards);
      const ecom: Record<string, number> = {};
      const atm: Record<string, number> = {};
      for (const c of cards) {
        const e = Number(c.dailyEcommerceLimit);
        const a = Number(c.dailyAtmLimit);
        ecom[c.id] = Number.isFinite(e) ? e : 2000;
        atm[c.id] = Number.isFinite(a) ? a : 500;
      }
      this.ecomLimits.set(ecom);
      this.atmLimits.set(atm);
    } finally {
      this.loading.set(false);
    }
  }

  async toggleFreeze(card: CardDto) {
    const frozen = card.status === 'Frozen';
    const path = frozen ? 'unfreeze' : 'freeze';
    try {
      const updated = await firstValueFrom(
        this.http.post<CardDto>(`${environment.apiBaseUrl}/api/cards/${card.id}/${path}`, {}, { headers: { 'Idempotency-Key': crypto.randomUUID() } })
      );
      this.cards.update((list) => list.map((c) => (c.id === card.id ? updated : c)));
    } catch {
      /* toast via interceptor */
    }
  }

  async reveal(id: string) {
    try {
      const res = await firstValueFrom(this.http.post<any>(`${environment.apiBaseUrl}/api/cards/${id}/reveal-cvv`, {}));
      this.cvvs.update((m) => ({ ...m, [id]: res.cvv }));
    } catch {
      /* rate-limited toast via interceptor */
    }
  }

  async saveLimits(id: string) {
    const card = this.cards().find((c) => c.id === id);
    if (!card || this.isSaving(id)) return;
    const ecom = this.ecomFor(card);
    const atm = this.atmFor(card);
    this.saving.update((m) => ({ ...m, [id]: true }));
    this.saveErrors.update((m) => ({ ...m, [id]: null }));
    try {
      const updated = await firstValueFrom(
        this.http.patch<CardDto>(`${environment.apiBaseUrl}/api/cards/${id}/limits`, {
          dailyEcommerceLimit: String(ecom),
          dailyAtmLimit: String(atm),
        })
      );
      this.cards.update((list) => list.map((c) => (c.id === id ? updated : c)));
      const e = Number(updated.dailyEcommerceLimit);
      const a = Number(updated.dailyAtmLimit);
      if (Number.isFinite(e)) this.setEcom(id, e);
      if (Number.isFinite(a)) this.setAtm(id, a);
    } catch {
      this.saveErrors.update((m) => ({ ...m, [id]: 'Could not save limits. Try again.' }));
      /* toast via interceptor */
    } finally {
      this.saving.update((m) => ({ ...m, [id]: false }));
    }
  }
}
