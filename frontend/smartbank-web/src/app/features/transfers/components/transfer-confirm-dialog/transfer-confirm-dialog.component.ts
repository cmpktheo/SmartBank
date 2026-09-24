import { Component, input, output } from '@angular/core';

@Component({
  selector: 'sb-transfer-confirm-dialog',
  standalone: true,
  templateUrl: './transfer-confirm-dialog.component.html',
  styleUrl: './transfer-confirm-dialog.component.scss',
})
export class TransferConfirmDialogComponent {
  from = input('');
  to = input('');
  amount = input('');
  currency = input('EUR');
  transferType = input('Internal');
  submitting = input(false);

  cancel = output<void>();
  confirm = output<void>();
}
