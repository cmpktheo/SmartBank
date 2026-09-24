import '@angular/compiler';
import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { CardsPage } from './cards.page';
import { CardsService } from './data-access/cards.service';
import type { CardDto } from '../../core/models/models';

const card1: CardDto = {
  id: 'card-1',
  accountId: 'acc-1',
  brand: 'Visa',
  maskedPan: '•••• 1234',
  lastFour: '1234',
  expiryMonth: 12,
  expiryYear: 2028,
  status: 'Active',
  dailyEcommerceLimit: '2000',
  dailyAtmLimit: '500',
  currency: 'EUR',
};

const frozenCard: CardDto = { ...card1, id: 'card-9', status: 'Frozen' };

function makePage(opts?: {
  cards?: CardDto[];
  freeze?: (id: string) => unknown;
  unfreeze?: (id: string) => unknown;
  revealCvv?: (id: string) => unknown;
  saveLimits?: (id: string, ecom: number, atm: number) => unknown;
}) {
  const cardsService = {
    getCards: () => of(opts?.cards ?? [card1]),
    freeze: (id: string) =>
      (opts?.freeze ? opts.freeze(id) : of({ ...card1, id, status: 'Frozen' })) as never,
    unfreeze: (id: string) =>
      (opts?.unfreeze ? opts.unfreeze(id) : of({ ...card1, id, status: 'Active' })) as never,
    revealCvv: (id: string) =>
      (opts?.revealCvv ? opts.revealCvv(id) : of({ cvv: '123' })) as never,
    saveLimits: (id: string, ecom: number, atm: number) =>
      (opts?.saveLimits
        ? opts.saveLimits(id, ecom, atm)
        : of({ ...card1, id, dailyEcommerceLimit: String(ecom), dailyAtmLimit: String(atm) })) as never,
  };
  const injector = Injector.create([{ provide: CardsService, useValue: cardsService }]);
  const page = runInInjectionContext(injector, () => new CardsPage());
  return { page, cardsService };
}

describe('CardsPage limits defaults', () => {
  it('ecomFor/atmFor fall back to 2000/500 on non-numeric limits', () => {
    const { page } = makePage({ cards: [] });
    const bad = { ...card1, dailyEcommerceLimit: 'n/a', dailyAtmLimit: 'abc' };
    expect(page.ecomFor(bad)).toBe(2000);
    expect(page.atmFor(bad)).toBe(500);
  });

  it('blank limits fall back to 2000/500 and stay clean', () => {
    const { page } = makePage({ cards: [] });
    const empty = { ...card1, dailyEcommerceLimit: '', dailyAtmLimit: '   ' };
    expect(page.ecomFor(empty)).toBe(2000);
    expect(page.atmFor(empty)).toBe(500);
    expect(page.isDirty(empty)).toBe(false);
  });

  it('ngOnInit seeds blank backend limits to fallbacks', async () => {
    const blank = { ...card1, id: 'blank', dailyEcommerceLimit: '', dailyAtmLimit: '' };
    const { page } = makePage({ cards: [blank] });
    await page.ngOnInit();
    expect(page.ecomFor(blank)).toBe(2000);
    expect(page.atmFor(blank)).toBe(500);
    expect(page.isDirty(page.cards()[0])).toBe(false);
  });

  it('explicit zero limits are preserved, not treated as blank', () => {
    const { page } = makePage({ cards: [] });
    const zero = { ...card1, dailyEcommerceLimit: '0', dailyAtmLimit: '0' };
    expect(page.ecomFor(zero)).toBe(0);
    expect(page.atmFor(zero)).toBe(0);
  });

  it('draft overrides stored limit and isDirty detects changes', async () => {
    const { page } = makePage();
    await page.ngOnInit();
    const card = page.cards()[0];
    expect(page.isDirty(card)).toBe(false);
    page.setEcom(card.id, 3000);
    expect(page.ecomFor(card)).toBe(3000);
    expect(page.isDirty(card)).toBe(true);
    page.setAtm(card.id, 600);
    expect(page.atmFor(card)).toBe(600);
  });

  it('ngOnInit seeds drafts from backend values', async () => {
    const { page } = makePage();
    await page.ngOnInit();
    expect(page.loading()).toBe(false);
    expect(page.ecomFor(card1)).toBe(2000);
    expect(page.atmFor(card1)).toBe(500);
    expect(page.isSaving(card1.id)).toBe(false);
    expect(page.saveErrorFor(card1.id)).toBeNull();
    expect(page.cvvFor(card1.id)).toBeNull();
  });
});

describe('CardsPage selection', () => {
  it('select/back toggles selectedCard', async () => {
    const { page } = makePage();
    await page.ngOnInit();
    expect(page.selectedCard()).toBeNull();
    page.select('card-1');
    expect(page.selectedCard()?.id).toBe('card-1');
    page.back();
    expect(page.selectedCard()).toBeNull();
  });
});

describe('CardsPage freeze/reveal', () => {
  it('toggleFreeze freezes an Active card', async () => {
    const { page } = makePage();
    await page.ngOnInit();
    await page.toggleFreeze({ ...card1, status: 'Active' });
    expect(page.cards()[0].status).toBe('Frozen');
  });

  it('toggleFreeze unfreezes a Frozen card', async () => {
    const { page } = makePage({ cards: [frozenCard] });
    await page.ngOnInit();
    await page.toggleFreeze(frozenCard);
    expect(page.cards()[0].status).toBe('Active');
  });

  it('toggleFreeze swallows backend errors (toast via interceptor)', async () => {
    const { page } = makePage({ freeze: () => throwError(() => new Error('down')) });
    await page.ngOnInit();
    await page.toggleFreeze({ ...card1, status: 'Active' });
    expect(page.cards()[0].status).toBe('Active');
  });

  it('reveal stores cvv per card', async () => {
    const { page } = makePage({ revealCvv: () => of({ cvv: '456' }) });
    await page.ngOnInit();
    expect(page.cvvFor('card-1')).toBeNull();
    await page.reveal('card-1');
    expect(page.cvvFor('card-1')).toBe('456');
  });

  it('reveal swallows rate-limit errors', async () => {
    const { page } = makePage({ revealCvv: () => throwError(() => new Error('429')) });
    await page.ngOnInit();
    await page.reveal('card-1');
    expect(page.cvvFor('card-1')).toBeNull();
  });
});

describe('CardsPage saveLimits', () => {
  it('success updates card and clears saving/error', async () => {
    const { page } = makePage();
    await page.ngOnInit();
    page.setEcom('card-1', 1500);
    page.setAtm('card-1', 300);
    await page.saveLimits('card-1');
    expect(page.cards()[0].dailyEcommerceLimit).toBe('1500');
    expect(page.cards()[0].dailyAtmLimit).toBe('300');
    expect(page.isSaving('card-1')).toBe(false);
    expect(page.saveErrorFor('card-1')).toBeNull();
    expect(page.isDirty(page.cards()[0])).toBe(false);
  });

  it('failure sets saveError and keeps drafts', async () => {
    const saveLimits = vi.fn(() => throwError(() => new Error('down')));
    const { page } = makePage({ saveLimits });
    await page.ngOnInit();
    page.setEcom('card-1', 1500);
    await page.saveLimits('card-1');
    expect(page.saveErrorFor('card-1')).toBe('Could not save limits. Try again.');
    expect(page.isSaving('card-1')).toBe(false);
    expect(page.ecomFor(page.cards()[0])).toBe(1500);
  });

  it('does nothing when already saving', async () => {
    const saveLimits = vi.fn(() => of({ ...card1 }));
    const { page } = makePage({ saveLimits });
    await page.ngOnInit();
    page.saving.update((m) => ({ ...m, ['card-1']: true }));
    await page.saveLimits('card-1');
    expect(saveLimits).not.toHaveBeenCalled();
  });
});
