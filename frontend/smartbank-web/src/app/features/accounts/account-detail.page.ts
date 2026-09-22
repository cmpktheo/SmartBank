import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { DatePipe } from '@angular/common';
import { environment } from '../../../environments/environment';
import { ComboBoxComponent, type ComboOption } from '../../shared/ui/combo-box.component';

@Component({
  selector: 'sb-account-detail',
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink, ComboBoxComponent],
  template: `
    <div class="sb-container tab-pane">
      <a routerLink="/accounts" class="muted" style="font-size:12px;text-decoration:none">← Back to accounts</a>
      <section class="card card-pad" aria-label="Statement">
        <div class="card-head">
          <div><h2 style="font-size:16px;color:#fff">Statement</h2><p class="muted" style="font-size:12px">Ledger transactions · CSV export supported</p></div>
          <button data-testid="export-statement-btn" (click)="exportCsv()" class="btn-secondary" style="font-size:12px">⬇ Export CSV</button>
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
        </form>
        @if (loading()) { <p class="muted" style="font-size:13px">Loading…</p> }
        <div class="sb-table-wrap">
          <table class="sb-table">
            <thead><tr><th>Reference</th><th>Date</th><th>Direction</th><th>Counterparty</th><th style="text-align:right">Amount</th></tr></thead>
            <tbody>
              @for (tx of items(); track tx.reference) {
                <tr data-testid="statement-row">
                  <td style="color:#fff;font-weight:600">{{ tx.reference }}</td>
                  <td class="muted tnum">{{ tx.bookedAt | date:'medium' }}</td>
                  <td><span class="pill" [class.pill-green]="tx.direction==='Credit'" [class.pill-blue]="tx.direction!=='Credit'">{{ tx.direction }}</span></td>
                  <td class="mono muted" style="font-size:11.5px">{{ tx.counterpartyIban ?? '—' }}</td>
                  <td class="tnum mono" style="text-align:right" [class.amount-credit]="tx.direction==='Credit'">{{ tx.direction === 'Credit' ? '+' : '−' }}{{ tx.amount }} {{ tx.currency }}</td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="muted" style="text-align:center;padding:24px">No transactions match your filters.</td></tr>
              }
            </tbody>
          </table>
        </div>
        <p class="muted" style="font-size:11.5px;margin-top:12px">Showing {{ items().length }} transactions · PDF export is not supported by the API — use CSV.</p>
      </section>
    </div>
  `,
})
export class AccountDetailPage implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  from = signal('');
  to = signal('');
  type = signal('All');
  items = signal<any[]>([]);
  id = signal('');
  loading = signal(false);

  readonly typeOptions: ComboOption[] = [
    { value: 'All', label: 'All activity', sub: 'Debits + credits', icon: '≡' },
    { value: 'Debit', label: 'Debits', sub: 'Money out', icon: '↑' },
    { value: 'Credit', label: 'Credits', sub: 'Money in', icon: '↓' },
  ];

  ngOnInit() {
    this.id.set(this.route.snapshot.paramMap.get('id') ?? '');
    this.load();
  }

  async load() {
    this.loading.set(true);
    try {
      const params = new URLSearchParams({ page: '1', pageSize: '20', type: this.type() });
      if (this.from()) params.set('from', new Date(this.from()).toISOString());
      if (this.to()) params.set('to', new Date(this.to()).toISOString());
      const res = await firstValueFrom(
        this.http.get<any>(`${environment.apiBaseUrl}/api/ledger/accounts/${this.id()}/transactions?${params}`)
      );
      this.items.set(res.items ?? []);
    } finally {
      this.loading.set(false);
    }
  }

  exportCsv() {
    const params = new URLSearchParams({ format: 'csv' });
    if (this.from()) params.set('from', new Date(this.from()).toISOString());
    if (this.to()) params.set('to', new Date(this.to()).toISOString());
    window.open(`${environment.apiBaseUrl}/api/ledger/accounts/${this.id()}/statement.csv?${params}`, '_blank');
  }
}
