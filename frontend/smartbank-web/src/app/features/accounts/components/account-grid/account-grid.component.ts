import { Component, input, output } from '@angular/core';
import type { AccountSummary } from '../../../../core/models/models';
import { AccountListCardComponent } from '../account-list-card/account-list-card.component';

@Component({
  selector: 'sb-account-grid',
  standalone: true,
  imports: [AccountListCardComponent],
  templateUrl: './account-grid.component.html',
  styleUrl: './account-grid.component.scss',
})
export class AccountGridComponent {
  accounts = input<AccountSummary[]>([]);
  openAccount = output<string>();
}
