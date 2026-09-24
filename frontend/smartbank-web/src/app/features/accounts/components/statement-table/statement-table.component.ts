import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { LucideArrowLeftRight, LucideCreditCard } from '@lucide/angular';
import type { RecentTx } from '../../data-access/ledger.models';

@Component({
  selector: 'sb-statement-table',
  standalone: true,
  imports: [DatePipe, LucideArrowLeftRight, LucideCreditCard],
  templateUrl: './statement-table.component.html',
  styleUrl: './statement-table.component.scss',
})
export class StatementTableComponent {
  items = input<RecentTx[]>([]);
  total = input(0);
  loading = input(false);
  loadingMore = input(false);
  hasMore = input(false);
  loadMore = output<void>();
}
