import { Component, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import type { AccountSummary } from '../../../../core/models/models';

@Component({
  selector: 'sb-account-card',
  standalone: true,
  imports: [CurrencyPipe],
  templateUrl: './account-card.component.html',
  styleUrl: './account-card.component.scss',
})
export class AccountCardComponent {
  account = input.required<AccountSummary>();
  open = output<string>();
}
