import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { LucideArrowLeftRight, LucideCreditCard } from '@lucide/angular';
import type { RecentTx } from '../../../accounts/data-access/ledger.models';

@Component({
  selector: 'sb-recent-transactions',
  standalone: true,
  imports: [DatePipe, LucideArrowLeftRight, LucideCreditCard],
  templateUrl: './recent-transactions.component.html',
  styleUrl: './recent-transactions.component.scss',
})
export class RecentTransactionsComponent {
  transactions = input<RecentTx[]>([]);
  accountAlias = input('All accounts');
  canViewStatement = input(false);
  viewStatement = output<void>();
}
