import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideCheck } from '@lucide/angular';
import { ComboBoxComponent, type ComboOption } from '../../../../shared/ui/combo-box.component';
import type { AccountSummary } from '../../../../core/models/models';
import type { IbanStatus } from '../../data-access/transfers.service';

@Component({
  selector: 'sb-transfer-form',
  standalone: true,
  imports: [FormsModule, ComboBoxComponent, LucideCheck],
  templateUrl: './transfer-form.component.html',
  styleUrl: './transfer-form.component.scss',
})
export class TransferFormComponent {
  accounts = input<AccountSummary[]>([]);
  sourceOptions = input<ComboOption[]>([]);
  selectedSourceId = input('');
  transferType = input('Internal');
  iban = input('');
  ibanStatus = input<IbanStatus>('unknown');
  holderName = input('');
  amount = input('');
  narrative = input('');
  sourceCurrency = input('EUR');
  sourceAlias = input('');
  insufficient = input(false);
  submitDisabled = input(true);

  selectedSourceIdChange = output<string>();
  transferTypeChange = output<string>();
  ibanChange = output<string>();
  amountChange = output<string>();
  narrativeChange = output<string>();
  review = output<void>();

  readonly transferTypes = ['Internal', 'Domestic', 'International'];
}
