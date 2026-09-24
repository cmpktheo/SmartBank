import { Component, computed, inject, signal, OnInit } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CardsService } from './data-access/cards.service';
import type { CardDto } from '../../core/models/models';
import { CardListComponent } from './components/card-list/card-list.component';
import { CardDetailComponent } from './components/card-detail/card-detail.component';

/** Coerce a backend limit string, falling back for blank/non-numeric values.
 *  Note: Number('') is 0 (finite), so blank must be checked explicitly. */
function parseLimit(raw: string | null | undefined, fallback: number): number {
  if (raw === null || raw === undefined || String(raw).trim() === '') return fallback;
  const parsed = Number(raw);
  return Number.isFinite(parsed) ? parsed : fallback;
}

@Component({
  selector: 'sb-cards',
  standalone: true,
  imports: [CardListComponent, CardDetailComponent],
  template: `
    <div class="sb-container tab-pane">
      <div>
        <h2 class="sb-page-title">Cards</h2>
        <p class="muted text-[12.5px]">
          Freeze / limits / CVV reveal — card issuance is automatic, full PAN is never exposed
        </p>
      </div>
      @if (loading()) {
        <p class="muted">Loading cards…</p>
      } @else {
        @if (selectedCard(); as card) {
          <sb-card-detail
            [card]="card"
            [cvv]="cvvFor(card.id)"
            [ecom]="ecomFor(card)"
            [atm]="atmFor(card)"
            [saving]="isSaving(card.id)"
            [dirty]="isDirty(card)"
            [saveError]="saveErrorFor(card.id)"
            (back)="back()"
            (reveal)="reveal($event)"
            (toggleFreeze)="toggleFreeze($event)"
            (ecomChange)="setEcom(card.id, $event)"
            (atmChange)="setAtm(card.id, $event)"
            (saveLimits)="saveLimits(card.id)"
          />
        } @else {
          <sb-card-list [cards]="cards()" (select)="select($event)" />
        }
      }
    </div>
  `,
})
export class CardsPage implements OnInit {
  private cardsService = inject(CardsService);

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
    return parseLimit(card.dailyEcommerceLimit, 2000);
  }

  atmFor(card: CardDto): number {
    const draft = this.atmLimits()[card.id];
    if (draft !== undefined) return draft;
    return parseLimit(card.dailyAtmLimit, 500);
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
    return (
      this.ecomFor(card) !== parseLimit(card.dailyEcommerceLimit, 2000) ||
      this.atmFor(card) !== parseLimit(card.dailyAtmLimit, 500)
    );
  }

  cvvFor(id: string): string | null {
    return this.cvvs()[id] ?? null;
  }

  async ngOnInit() {
    try {
      const cards = await firstValueFrom(this.cardsService.getCards());
      this.cards.set(cards);
      const ecom: Record<string, number> = {};
      const atm: Record<string, number> = {};
      for (const c of cards) {
        ecom[c.id] = parseLimit(c.dailyEcommerceLimit, 2000);
        atm[c.id] = parseLimit(c.dailyAtmLimit, 500);
      }
      this.ecomLimits.set(ecom);
      this.atmLimits.set(atm);
    } finally {
      this.loading.set(false);
    }
  }

  async toggleFreeze(card: CardDto) {
    const frozen = card.status === 'Frozen';
    try {
      const updated = await firstValueFrom(
        frozen ? this.cardsService.unfreeze(card.id) : this.cardsService.freeze(card.id)
      );
      this.cards.update((list) => list.map((c) => (c.id === card.id ? updated : c)));
    } catch {
      /* toast via interceptor */
    }
  }

  async reveal(id: string) {
    try {
      const res = await firstValueFrom(this.cardsService.revealCvv(id));
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
      const updated = await firstValueFrom(this.cardsService.saveLimits(id, ecom, atm));
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
