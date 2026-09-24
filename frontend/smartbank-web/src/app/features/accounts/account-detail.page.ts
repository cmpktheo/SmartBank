import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AccountSelectionStore } from './account-selection.store';
import { LedgerService } from './data-access/ledger.service';
import type { RecentTx } from './data-access/ledger.models';
import type { ComboOption } from '../../shared/ui/combo-box.component';
import { LucideDownload, LucideArrowLeft } from '@lucide/angular';
import { StatementFiltersComponent } from './components/statement-filters/statement-filters.component';
import { StatementTableComponent } from './components/statement-table/statement-table.component';

@Component({
  selector: 'sb-account-detail',
  standalone: true,
  imports: [
    RouterLink,
    LucideDownload,
    LucideArrowLeft,
    StatementFiltersComponent,
    StatementTableComponent,
  ],
  template: `
    <div class="sb-container tab-pane">
      <a
        routerLink="/accounts"
        class="muted text-[12px] no-underline inline-flex items-center gap-1"
        ><svg lucideArrowLeft class="size-3" /> Back to accounts</a
      >
      <section class="card card-pad" aria-label="Statement">
        <div class="card-head">
          <div>
            <h2 class="sb-section-title-lg">Statement</h2>
            <p class="muted text-[12px]">Ledger transactions · CSV export supported</p>
          </div>
          <button
            data-testid="export-statement-btn"
            (click)="exportCsv()"
            class="btn-secondary text-[12px]"
          >
            <svg lucideDownload class="size-3.5" /> Export CSV
          </button>
        </div>
        <sb-statement-filters
          [from]="from()"
          [to]="to()"
          [type]="type()"
          [kind]="kind()"
          [typeOptions]="typeOptions"
          [kindOptions]="kindOptions"
          (fromChange)="from.set($event)"
          (toChange)="to.set($event)"
          (typeChange)="type.set($event); load()"
          (kindChange)="kind.set($event); load()"
          (apply)="load()"
        />
        <sb-statement-table
          [items]="items()"
          [total]="total()"
          [loading]="loading()"
          [loadingMore]="loadingMore()"
          [hasMore]="hasMore()"
          (loadMore)="loadMore()"
        />
      </section>
    </div>
  `,
})
export class AccountDetailPage implements OnInit {
  private ledger = inject(LedgerService);
  private router = inject(Router);
  private selection = inject(AccountSelectionStore);

  from = signal('');
  to = signal('');
  type = signal('All');
  kind = signal('All');
  items = signal<RecentTx[]>([]);
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
      const res = await firstValueFrom(
        this.ledger.getTransactions(this.id(), {
          page: 1,
          pageSize: this.pageSize,
          type: this.type(),
          kind: this.kind(),
          from: this.from(),
          to: this.to(),
        })
      );
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
      const res = await firstValueFrom(
        this.ledger.getTransactions(this.id(), {
          page: next,
          pageSize: this.pageSize,
          type: this.type(),
          kind: this.kind(),
          from: this.from(),
          to: this.to(),
        })
      );
      this.page.set(next);
      this.items.update((list) => [...list, ...(res.items ?? [])]);
      this.total.set(res.total ?? this.total());
    } finally {
      this.loadingMore.set(false);
    }
  }

  exportCsv() {
    window.open(this.ledger.statementCsvUrl(this.id(), this.from(), this.to()), '_blank');
  }
}
