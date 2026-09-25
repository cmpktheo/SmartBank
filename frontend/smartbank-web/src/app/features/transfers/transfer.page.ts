import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LucideCheck } from '@lucide/angular';
import { formatIban, isValidIban } from '../../core/iban';
import { AccountsService } from '../accounts/data-access/accounts.service';
import {
  TransfersService,
  type IbanStatus,
} from './data-access/transfers.service';
import type { ComboOption } from '../../shared/ui/combo-box.component';
import type { AccountSummary } from '../../core/models/models';
import { TransferFormComponent } from './components/transfer-form/transfer-form.component';
import { TransferConfirmDialogComponent } from './components/transfer-confirm-dialog/transfer-confirm-dialog.component';

const CURRENCY_GLYPH: Record<string, string> = { EUR: '€', USD: '$', GBP: '£' };

@Component({
  selector: 'sb-transfer',
  standalone: true,
  imports: [TransferFormComponent, TransferConfirmDialogComponent, LucideCheck],
  template: `
    <div class="sb-container sb-narrow tab-pane">
      <div>
        <h2 class="sb-page-title">Send money</h2>
        <p class="muted text-[12.5px]">
          Internal transfers settle instantly · International/Domestic external wires are rejected
          by the API
        </p>
      </div>

      <sb-transfer-form
        [accounts]="accounts()"
        [sourceOptions]="sourceOptions()"
        [selectedSourceId]="selectedSourceId()"
        [transferType]="transferType()"
        [iban]="iban()"
        [ibanStatus]="ibanStatus()"
        [holderName]="holderName()"
        [amount]="amount()"
        [narrative]="narrative()"
        [sourceCurrency]="sourceAccount()?.currency ?? 'EUR'"
        [sourceAlias]="sourceAlias()"
        [insufficient]="insufficient()"
        [submitDisabled]="submitDisabled()"
        (selectedSourceIdChange)="selectedSourceId.set($event)"
        (transferTypeChange)="transferType.set($event)"
        (ibanChange)="iban.set($event); onIbanChange()"
        (amountChange)="amount.set($event)"
        (narrativeChange)="narrative.set($event)"
        (review)="openConfirm()"
      />

      @if (confirmOpen()) {
        <sb-transfer-confirm-dialog
          [from]="sourceAlias()"
          [to]="formatIban(iban())"
          [amount]="amount()"
          [currency]="sourceAccount()?.currency ?? 'EUR'"
          [transferType]="transferType()"
          [submitting]="submitting()"
          (cancel)="confirmOpen.set(false)"
          (confirm)="confirm()"
        />
      }

      @if (successReference()) {
        <div data-testid="transfer-success-msg" class="sb-alert sb-alert-ok flex items-center gap-2" role="status">
          <svg lucideCheck class="size-3.5" /> Transfer booked. Reference:
          <strong data-testid="transfer-reference" class="mono">{{ successReference() }}</strong>
        </div>
      }
      @if (apiError()) {
        <div class="sb-alert sb-alert-error" role="alert">{{ apiError() }}</div>
      }
    </div>
  `,
})
export class TransferPage implements OnInit {
  private accountsService = inject(AccountsService);
  private transfers = inject(TransfersService);

  accounts = signal<AccountSummary[]>([]);
  selectedSourceId = signal('');
  transferType = signal('Internal');
  iban = signal('');
  ibanStatus = signal<IbanStatus>('unknown');
  holderName = signal('');
  amount = signal('');
  narrative = signal('');
  confirmOpen = signal(false);
  successReference = signal('');
  submitting = signal(false);
  apiError = signal<string | null>(null);

  sourceAccount = computed(() => this.accounts().find((a) => a.id === this.selectedSourceId()));
  sourceAlias = computed(() => this.sourceAccount()?.alias ?? '');
  sourceOptions = computed<ComboOption[]>(() =>
    this.accounts().map((a) => ({
      value: a.id,
      label: `${a.alias} · ${a.availableBalance} ${a.currency}`,
      sub: `${a.type} · …${a.ibanFormatted.slice(-4)} · ${a.status}`,
      icon: CURRENCY_GLYPH[a.currency] ?? 'wallet',
    }))
  );
  insufficient = computed(() => {
    const acc = this.sourceAccount();
    if (!acc || !this.amount()) return false;
    return Number(this.amount()) > Number(acc.availableBalance);
  });
  submitDisabled = computed(
    () =>
      !this.selectedSourceId() ||
      !this.amount() ||
      Number(this.amount()) <= 0 ||
      this.ibanStatus() === 'invalid' ||
      this.insufficient() ||
      this.submitting()
  );

  formatIban = formatIban;

  async ngOnInit() {
    const accounts = await firstValueFrom(this.accountsService.getAccounts());
    this.accounts.set(accounts);
    if (accounts[0]) this.selectedSourceId.set(accounts[0].id);
  }

  private debounce: ReturnType<typeof setTimeout> | undefined;
  onIbanChange() {
    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.verifyIban(), 300);
  }

  async verifyIban() {
    const raw = this.iban();
    if (!raw) {
      this.ibanStatus.set('unknown');
      return;
    }
    if (!isValidIban(raw)) {
      this.ibanStatus.set('invalid');
      return;
    }
    try {
      const res = await firstValueFrom(this.transfers.verifyIban(raw));
      if (res.verified) {
        this.ibanStatus.set('verified');
        this.holderName.set(res.accountHolderName ?? '');
      } else this.ibanStatus.set('unverified');
    } catch {
      this.ibanStatus.set('valid');
    }
  }

  openConfirm() {
    if (this.submitDisabled()) return;
    this.apiError.set(null);
    this.confirmOpen.set(true);
    setTimeout(
      () =>
        (document.querySelector('[data-testid="transfer-confirm-btn"]') as HTMLElement | null)?.focus?.(),
      0
    );
  }

  async confirm() {
    this.submitting.set(true);
    this.apiError.set(null);
    try {
      const res = await firstValueFrom(
        this.transfers.bookTransfer({
          sourceAccountId: this.selectedSourceId(),
          destinationIban: this.iban(),
          amount: this.amount(),
          currency: this.sourceAccount()?.currency ?? 'EUR',
          transferType: this.transferType(),
          narrative: this.narrative(),
        })
      );
      this.confirmOpen.set(false);
      this.successReference.set(res.reference);
    } catch (e: unknown) {
      const detail = (e as { error?: { detail?: string } })?.error?.detail;
      this.apiError.set(detail ?? 'Transfer failed.');
    } finally {
      this.submitting.set(false);
    }
  }
}
