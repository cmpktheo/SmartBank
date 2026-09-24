import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ComboBoxComponent, type ComboOption } from '../../../../shared/ui/combo-box.component';

export interface CreateAccountPayload {
  alias: string;
  type: string;
  currency: string;
}

@Component({
  selector: 'sb-account-create-form',
  standalone: true,
  imports: [FormsModule, ComboBoxComponent],
  templateUrl: './account-create-form.component.html',
  styleUrl: './account-create-form.component.scss',
})
export class AccountCreateFormComponent {
  creating = input(false);
  error = input<string | null>(null);
  create = output<CreateAccountPayload>();

  alias = signal('');
  type = signal('Current');
  currency = signal('EUR');

  readonly typeOptions: ComboOption[] = [
    { value: 'Current', label: 'Current', sub: 'Everyday payments', icon: 'wallet' },
    { value: 'Savings', label: 'Savings', sub: 'Set money aside', icon: 'circle' },
  ];
  readonly currencyOptions: ComboOption[] = [
    { value: 'EUR', label: 'EUR — Euro', sub: 'European accounts', icon: '€' },
    { value: 'USD', label: 'USD — US Dollar', sub: 'US accounts', icon: '$' },
    { value: 'GBP', label: 'GBP — British Pound', sub: 'UK accounts', icon: '£' },
  ];

  submit() {
    if (!this.alias() || this.creating()) return;
    this.create.emit({ alias: this.alias(), type: this.type(), currency: this.currency() });
  }

  reset() {
    this.alias.set('');
  }
}
