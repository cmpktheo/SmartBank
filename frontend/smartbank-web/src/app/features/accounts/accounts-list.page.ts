import { Component, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { AccountSelectionStore } from './account-selection.store';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { CurrencyPipe } from '@angular/common';
import { environment } from '../../../environments/environment';
import { ComboBoxComponent, type ComboOption } from '../../shared/ui/combo-box.component';
import { LucideArrowRight } from '@lucide/angular';
import type { AccountSummary } from '../../core/models/models';

@Component({
  selector: 'sb-accounts-list',
  standalone: true,
  imports: [FormsModule, CurrencyPipe, ComboBoxComponent, LucideArrowRight],
  template: `
    <div class="sb-container tab-pane">
      <div class="row-between" style="flex-wrap:wrap">
        <div><h2 style="font-size:22px;color:#fff">Accounts</h2><p class="muted" style="font-size:12.5px">Your IBAN accounts · {{ accounts().length }} total</p></div>
        <button class="btn-primary" style="font-size:12.5px" (click)="showNew.set(!showNew())">+ Open new account</button>
      </div>

      @if (showNew()) {
        <form class="card card-pad" (ngSubmit)="openAccount()" style="display:flex;gap:12px;flex-wrap:wrap;align-items:flex-end">
          <div style="flex:1;min-width:160px"><label class="sb-label" for="new-alias">Alias</label>
            <input id="new-alias" data-testid="account-new-alias" class="field" [(ngModel)]="alias" name="alias" required placeholder="e.g. Holiday fund" /></div>
          <div><label class="sb-label" id="new-type-label">Type</label>
            <sb-combo-box [options]="typeOptions" [value]="type()" (valueChange)="type.set($event)"
              heading="Account type" ariaLabel="Account type" buttonTestid="account-new-type-combo" optionTestidPrefix="account-new-type-option-" />
            <select id="new-type" data-testid="account-new-type" class="sr-only" tabindex="-1" aria-labelledby="new-type-label" [(ngModel)]="type" name="type"><option>Current</option><option>Savings</option></select></div>
          <div><label class="sb-label" id="new-cur-label">Currency</label>
            <sb-combo-box [options]="currencyOptions" [value]="currency()" (valueChange)="currency.set($event)"
              heading="Currency" ariaLabel="Currency" buttonTestid="account-new-currency-combo" optionTestidPrefix="account-new-currency-option-" />
            <select id="new-cur" data-testid="account-new-currency" class="sr-only" tabindex="-1" aria-labelledby="new-cur-label" [(ngModel)]="currency" name="currency"><option>EUR</option><option>USD</option><option>GBP</option></select></div>
          <button data-testid="account-new-submit" class="btn-primary" type="submit" [disabled]="creating() || !alias()">{{ creating() ? 'Opening…' : 'Open account' }}</button>
          @if (error()) { <div data-testid="account-new-error" class="sb-alert sb-alert-error" role="alert">{{ error() }}</div> }
        </form>
      }

      <div class="grid-3">
        @for (a of accounts(); track a.id) {
          <article class="card card-pad card-hover" data-testid="dashboard-account-card" (click)="openDetail(a.id)" (keydown.enter)="openDetail(a.id)" style="cursor:pointer" tabindex="0" role="link" [attr.aria-label]="'Open ' + a.alias">
            <div class="row-between" style="margin-bottom:12px">
              <span class="micro-label" style="color:#93c5fd">{{ a.type }} · {{ a.currency }}</span>
              <span class="pill" [class.pill-green]="a.status==='Active'" [class.pill-amber]="a.status==='Frozen'" [class.pill-red]="a.status==='Closed'">{{ a.status }}</span>
            </div>
            <div data-testid="dashboard-account-alias" style="font-size:14px;font-weight:600;color:#fff">{{ a.alias }}</div>
            <div data-testid="dashboard-account-iban" class="mono muted" style="font-size:11.5px">{{ a.ibanFormatted }}</div>
            <p class="tnum" data-testid="dashboard-account-balance" style="font-size:28px;font-weight:700;color:#fff;font-family:Outfit,sans-serif;margin-top:6px">{{ +a.availableBalance | currency: a.currency }}</p>
            <div class="muted" style="font-size:11.5px">Available · posted {{ +a.postedBalance | currency: a.currency }}</div>
            <div class="divider row-between muted mono" style="font-size:11.5px"><span>{{ a.ibanFormatted.slice(-4) }}</span><span style="display:inline-flex;align-items:center;gap:4px">View statement <svg lucideArrowRight style="width:12px;height:12px" /></span></div>
          </article>
        } @empty {
          <p class="muted">No accounts yet.</p>
        }
      </div>
    </div>
  `,
})
export class AccountsListPage implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private selection = inject(AccountSelectionStore);
  accounts = signal<AccountSummary[]>([]);
  showNew = signal(false);
  alias = signal('');
  type = signal('Current');
  currency = signal('EUR');
  creating = signal(false);
  error = signal<string | null>(null);

  readonly typeOptions: ComboOption[] = [
    { value: 'Current', label: 'Current', sub: 'Everyday payments', icon: 'wallet' },
    { value: 'Savings', label: 'Savings', sub: 'Set money aside', icon: 'circle' },
  ];
  readonly currencyOptions: ComboOption[] = [
    { value: 'EUR', label: 'EUR — Euro', sub: 'European accounts', icon: '€' },
    { value: 'USD', label: 'USD — US Dollar', sub: 'US accounts', icon: '$' },
    { value: 'GBP', label: 'GBP — British Pound', sub: 'UK accounts', icon: '£' },
  ];

  async ngOnInit() {
    const accounts = await firstValueFrom(this.http.get<AccountSummary[]>(`${environment.apiBaseUrl}/api/accounts`));
    this.accounts.set(accounts);
  }

  openDetail(id: string) {
    this.selection.select(id);
    this.router.navigate(['/accounts/detail']);
  }

  async openAccount() {
    this.creating.set(true);
    this.error.set(null);
    try {
      const created = await firstValueFrom(
        this.http.post<AccountSummary>(`${environment.apiBaseUrl}/api/accounts`, {
          alias: this.alias(), type: this.type(), currency: this.currency(),
        })
      );
      this.accounts.update((l) => [...l, created]);
      this.showNew.set(false);
      this.alias.set('');
    } catch (e: any) {
      this.error.set(e.error?.detail ?? 'Could not open account.');
    } finally {
      this.creating.set(false);
    }
  }
}
