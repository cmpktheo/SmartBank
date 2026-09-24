import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AccountSelectionStore } from '../accounts/account-selection.store';
import { AccountsService } from '../accounts/data-access/accounts.service';
import { LedgerService } from '../accounts/data-access/ledger.service';
import type { RecentTx } from '../accounts/data-access/ledger.models';
import type { AccountSummary } from '../../core/models/models';
import { BalanceOverviewComponent } from './components/balance-overview/balance-overview.component';
import { RecentTransactionsComponent } from './components/recent-transactions/recent-transactions.component';

@Component({
  selector: 'sb-dashboard',
  standalone: true,
  imports: [BalanceOverviewComponent, RecentTransactionsComponent],
  styleUrl: './dashboard.page.scss',
  template: `
    <div class="sb-container tab-pane">
      <sb-balance-overview
        [accounts]="accounts()"
        [totals]="totalsByCurrency()"
        [loading]="loading()"
        (openAccount)="openDetail($event)"
      />
      <sb-recent-transactions
        [transactions]="recent()"
        [accountAlias]="recentAccountAlias()"
        [canViewStatement]="!!firstAccountId()"
        (viewStatement)="openDetail(firstAccountId())"
      />
    </div>
  `,
})
export class DashboardPage implements OnInit {
  private accountsService = inject(AccountsService);
  private ledger = inject(LedgerService);
  private router = inject(Router);
  private selection = inject(AccountSelectionStore);

  accounts = signal<AccountSummary[]>([]);
  recent = signal<RecentTx[]>([]);
  loading = signal(true);

  firstAccountId = computed(() => this.ledger.preferCurrentAccount(this.accounts())?.id ?? '');
  recentAccountAlias = computed(
    () => this.ledger.preferCurrentAccount(this.accounts())?.alias ?? 'All accounts'
  );
  totalsByCurrency = computed(() => this.ledger.totalsByCurrency(this.accounts()));

  openDetail(id: string) {
    if (!id) return;
    this.selection.select(id);
    this.router.navigate(['/accounts/detail']);
  }

  async ngOnInit() {
    try {
      const accounts = await firstValueFrom(this.accountsService.getAccounts());
      this.accounts.set(accounts);
      const first = this.ledger.preferCurrentAccount(accounts);
      if (first) {
        const recent = await firstValueFrom(this.ledger.getRecent(first.id));
        this.recent.set(recent);
      }
    } finally {
      this.loading.set(false);
    }
  }
}
