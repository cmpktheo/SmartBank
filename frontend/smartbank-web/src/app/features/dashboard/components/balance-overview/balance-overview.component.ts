import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { LucideArrowUpRight, LucideCreditCard, LucideWallet } from '@lucide/angular';
import type { AccountSummary } from '../../../../core/models/models';
import type { CurrencyTotal } from '../../../accounts/data-access/ledger.models';
import { AccountCardComponent } from '../account-card/account-card.component';

@Component({
  selector: 'sb-balance-overview',
  standalone: true,
  imports: [
    RouterLink,
    CurrencyPipe,
    LucideArrowUpRight,
    LucideCreditCard,
    LucideWallet,
    AccountCardComponent,
  ],
  templateUrl: './balance-overview.component.html',
  styleUrl: './balance-overview.component.scss',
})
export class BalanceOverviewComponent {
  accounts = input<AccountSummary[]>([]);
  totals = input<CurrencyTotal[]>([]);
  loading = input(false);
  openAccount = output<string>();
}
