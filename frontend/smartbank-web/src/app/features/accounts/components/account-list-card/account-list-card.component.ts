import { Component, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { LucideArrowRight } from '@lucide/angular';
import type { AccountSummary } from '../../../../core/models/models';

@Component({
  selector: 'sb-account-list-card',
  standalone: true,
  imports: [CurrencyPipe, LucideArrowRight],
  templateUrl: './account-list-card.component.html',
  styleUrl: './account-list-card.component.scss',
})
export class AccountListCardComponent {
  account = input.required<AccountSummary>();
  open = output<string>();
}
