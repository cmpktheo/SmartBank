import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { AccountSelectionStore } from './account-selection.store';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { DatePipe } from '@angular/common';
import { environment } from '../../../environments/environment';
import { ComboBoxComponent, type ComboOption } from '../../shared/ui/combo-box.component';
import { LucideDownload, LucideArrowLeft, LucideArrowLeftRight, LucideCreditCard } from '@lucide/angular';

@Component({
  selector: 'sb-account-detail',
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink, ComboBoxComponent, LucideDownload, LucideArrowLeft, LucideArrowLeftRight, LucideCreditCard],
  template: `
    <div class="sb-container tab-pane">
      <a routerLink="/accounts" class="muted" style="font-size:12px;text-decoration:none;display:inline-flex;align-items:center;gap:4px"><svg lucideArrowLeft style="width:12px;height:12px" /> Back to accounts</a>
      <section class="card card-pad" aria-label="Statement">
        <div class="card-head">
          <div><h2 style="font-size:16px;color:#fff">Statement</h2><p class="muted" style="font-size:12px">Ledger transactions · CSV export supported</p></div>
          <button data-testid="export-statement-btn" (click)="exportCsv()" class="btn-secondary" style="font-size:12px"><svg lucideDownload style="width:14px;height:14px" /> Export CSV</button>
        </div>
        <form (ngSubmit)="load()" style="display:flex;gap:10px;flex-wrap:wrap;margin-bottom:16px">
          <div><label class="sb-label" for="f-from">From</label>
            <input id="f-from" data-testid="statement-filter-from" type="date" class="field" [(ngModel)]="from" name="from" /></div>
          <div><label class="sb-label" for="f-to">To</label>
            <input id="f-to" data-testid="statement-filter-to" type="date" class="field" [(ngModel)]="to" name="to" /></div>
          <div><label class="sb-label" id="f-type-label">Type</label>
            <sb-combo-box
              [options]="typeOptions"
              [value]="type()"
              (valueChange)="type.set($event); load()"
              heading="Status"
              ariaLabel="Filter by status"
              buttonTestid="statement-filter-combo"
              optionTestidPrefix="statement-filter-option-"
            />
            <!-- Native select stays in sync (sr-only): keeps selectOption e2e + AT working -->
            <select id="f-type" data-testid="statement-filter-type" class="sr-only" tabindex="-1" aria-labelledby="f-type-label" [(ngModel)]="type" name="type">
              <option>All</option><option>Debit</option><option>Credit</option>
            </select></div>
          <div style="align-self:flex-end"><button data-testid="statement-apply-btn" type="submit" class="btn-primary">Apply</button></div>
          <div><label class="sb-label" id="f-kind-label">Kind</label>
            <sb-combo-box
              [options]="kindOptions"
              [value]="kind()"
              (valueChange)="kind.set($event); load()"
              heading="Kind"
              ariaLabel="Filter by kind"
              buttonTestid="statement-kind-combo"
              optionTestidPrefix="statement-kind-option-"
            />
            <select id="f-kind" data-testid="statement-kind" class="sr-only" tabindex="-1" aria-labelledby="f-kind-label" [(ngModel)]="kind" name="kind">
              <option>All</option><option>Transfer</option><option>CardPayment</option>
            </select></div>
        </form>
        @if (loading()) { <p class="muted" style="font-size:13px">Loading…</p> }
        <div class="sb-table-wrap">
          <table class="sb-table">
            <thead><tr><th>Reference</th><th>Date</th><th>Direction</th><th>Kind</th><th>Counterparty</th><th style="text-align:right">Amount</th><th style="text-align:right">Balance</th></tr></thead>
            <tbody>
              @for (tx of items(); track tx.transactionId) {
                <tr data-testid="statement-row">
                  <td style="color:#fff;font-weight:600">{{ tx.reference }}</td>
                  <td class="muted tnum">{{ tx.bookedAt | date:'medium' }}</td>
                  <td><span class="pill" [class.pill-green]="tx.direction==='Credit'" [class.pill-blue]="tx.direction!=='Credit'">{{ tx.direction }}</span></td>
                  <td>@if (tx.kind === 'CardPayment') {
                    <span class="pill pill-blue"><svg lucideCreditCard style="width:11px;height:11px" /> Card</span>
                  } @else {
                    <span class="pill"><svg lucideArrowLeftRight style="width:11px;height:11px" /> Transfer</span>
                  }</td>
                  <td class="mono muted" style="font-size:11.5px">{{ tx.counterpartyIban ?? '—' }}</td>
                  <td class="tnum mono" style="text-align:right" [class.amount-credit]="tx.direction==='Credit'">{{ tx.direction === 'Credit' ? '+' : '−' }}{{ tx.amount }} {{ tx.currency }}</td>
                  <td data-testid="statement-balance" class="tnum mono" style="text-align:right">{{ tx.balanceAfter }} {{ tx.currency }}</td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="muted" style="text-align:center;padding:24px">No transactions match your filters.</td></tr>
              }
            </tbody>
          </table>
        </div>
        <p class="muted" style="font-size:11.5px;margin-top:12px">Showing {{ items().length }} of {{ total() }} transactions · PDF export is not supported by the API — use CSV.</p>
        @if (hasMore()) {
          <button data-testid="statement-load-more-btn" class="btn-secondary" style="margin-top:8px;font-size:12px" (click)="loadMore()" [disabled]="loadingMore()">{{ loadingMore() ? 'Loading…' : 'Load more' }}</button>
        }
      </section>
    </div>
  `,
})
export class AccountDetailPage implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private selection = inject(AccountSelectionStore);
  from = signal('');
  to = signal('');
  type = signal('All');
  kind = signal('All');
  items = signal<any[]>([]);
  id = signal('');
  loading = signal(false);
  page = signal(1);
  total = signal(0);
  loadingMore = signal(false);
  readonly pageSize = 20;
  hasMore = computed(() => this.items().length < this.total());

  readonly typeOptions: ComboOption[] = [
{ value: 'All', label: 'All activity', sub: 'Debits + credits', icon: 'layout-grid' },
     { value: 'Debit', label: 'Debits', sub: 'Money out', icon: 'arrow-up' },
     { value: 'Credit', label: 'Credits', sub: 'Money in', icon: 'arrow-down' },
  ];

  readonly kindOptions: ComboOption[] = [
    { value: 'All', label: 'All kinds', sub: 'Transfers + cards', icon: 'layout-grid' },
    { value: 'Transfer', label: 'Bank transfers', sub: 'Transfers only', icon: 'arrow-left-right' },
    { value: 'CardPayment', label: 'Card payments', sub: 'Card spend + refunds', icon: 'credit-card' },
  ];

  ngOnInit() {
    const selected = this.selection.selectedId();
    if (!selected) {
      this.router.navigate(['/accounts'], { replaceUrl: true });
      return;
    }
    this.id.set(selected);
    this.load();
  }

  async load() {
    this.page.set(1);
    this.loading.set(true);
    try {
      const res = await this.fetchPage(1);
      this.items.set(res.items ?? []);
      this.total.set(res.total ?? 0);
    } finally {
      this.loading.set(false);
    }
  }

  async loadMore() {
    if (this.loadingMore() || !this.hasMore()) return;
    const next = this.page() + 1;
    this.loadingMore.set(true);
    try {
      const res = await this.fetchPage(next);
      this.page.set(next);
      this.items.update((list) => [...list, ...(res.items ?? [])]);
      this.total.set(res.total ?? this.total());
    } finally {
      this.loadingMore.set(false);
    }
  }

  private fetchPage(page: number) {
    const params = new URLSearchParams({ page: String(page), pageSize: String(this.pageSize), type: this.type(), kind: this.kind() });
    if (this.from()) params.set('from', new Date(this.from()).toISOString());
    if (this.to()) params.set('to', new Date(this.to()).toISOString());
    return firstValueFrom(
      this.http.get<any>(`${environment.apiBaseUrl}/api/ledger/accounts/${this.id()}/transactions?${params}`)
    );
  }

  exportCsv() {
    const params = new URLSearchParams({ format: 'csv' });
    if (this.from()) params.set('from', new Date(this.from()).toISOString());
    if (this.to()) params.set('to', new Date(this.to()).toISOString());
    window.open(`${environment.apiBaseUrl}/api/ledger/accounts/${this.id()}/statement.csv?${params}`, '_blank');
  }
}
