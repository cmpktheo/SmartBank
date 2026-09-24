import { Component, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { compactIban, isValidIban, formatIban } from '../../core/iban';
import { ComboBoxComponent, type ComboOption } from '../../shared/ui/combo-box.component';
import type { AccountSummary } from '../../core/models/models';
import { LucideCheck } from '@lucide/angular';

const CURRENCY_GLYPH: Record<string, string> = { EUR: '€', USD: '$', GBP: '£' };

@Component({
  selector: 'sb-transfer',
  standalone: true,
  imports: [FormsModule, ComboBoxComponent, LucideCheck],
  template: `
    <div class="sb-container tab-pane" style="max-width:720px">
      <div><h2 style="font-size:22px;color:#fff">Send money</h2>
        <p class="muted" style="font-size:12.5px">Internal transfers settle instantly · International/Domestic external wires are rejected by the API</p></div>

      <section class="card card-pad">
        <div role="tablist" aria-label="Transfer type" style="display:flex;gap:6px;background:rgba(0,0,0,.3);padding:4px;border-radius:10px;border:1px solid rgba(255,255,255,.07);margin-bottom:20px">
          @for (t of ['Internal','Domestic','International']; track t) {
            <button [attr.data-testid]="'transfer-type-' + t.toLowerCase()" (click)="transferType.set(t)"
              [style.background]="transferType()===t ? '#2563eb' : 'transparent'"
              [style.color]="transferType()===t ? '#fff' : '#94a3b8'"
              style="flex:1;padding:8px;border:none;border-radius:8px;font-size:12.5px;font-weight:600;cursor:pointer">{{ t }}</button>
          }
        </div>

        <div class="sb-form-row">
          <div><label class="sb-label" id="t-src-label">Source account</label>
            <sb-combo-box
              [options]="sourceOptions()"
              [value]="selectedSourceId()"
              (valueChange)="selectedSourceId.set($event)"
              [heading]="'Source account · ' + accounts().length"
              [footer]="'Settles instantly · no fee'"
              ariaLabel="Source account"
              buttonTestid="transfer-source-select"
              optionTestidPrefix="transfer-source-option-"
            />
            <!-- Hidden native select keeps form/AT semantics in sync -->
            <select class="sr-only" tabindex="-1" aria-hidden="true" aria-labelledby="t-src-label" [(ngModel)]="selectedSourceId">
              @for (a of accounts(); track a.id) {
                <option [value]="a.id">{{ a.alias }} ({{ a.availableBalance }} {{ a.currency }})</option>
              }
            </select></div>

          <div><label class="sb-label" for="t-iban">Destination IBAN</label>
            <input id="t-iban" data-testid="transfer-iban-input" class="field mono" [(ngModel)]="iban" (ngModelChange)="onIbanChange()" placeholder="CH93…" autocomplete="off" />
            @if (ibanStatus() === 'verified') {
              <div data-testid="iban-verified-badge" class="sb-alert sb-alert-ok" style="margin-top:8px"><svg lucideCheck style="width:14px;height:14px" /> Beneficiary verified — {{ holderName() }}</div>
            }
            @if (ibanStatus() === 'invalid') {
              <div data-testid="iban-invalid-msg" class="sb-alert sb-alert-error" style="margin-top:8px">IBAN checksum is invalid.</div>
            }
            @if (ibanStatus() === 'unverified') {
              <div class="muted" style="font-size:12px;margin-top:8px">IBAN format is valid but the holder could not be verified — transfer may be rejected.</div>
            }
          </div>

          <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
            <div><label class="sb-label" for="t-amt">Amount ({{ sourceAccount()?.currency ?? 'EUR' }})</label>
              <div class="input-with-prefix"><span>$</span>
                <input id="t-amt" data-testid="transfer-amount-input" class="field tnum" [(ngModel)]="amount" inputmode="decimal" placeholder="1,000.00" /></div></div>
            <div><label class="sb-label" for="t-nar">Reference <span class="muted">(optional)</span></label>
              <input id="t-nar" data-testid="transfer-narrative-input" class="field" [(ngModel)]="narrative" maxlength="140" placeholder="Invoice #4922" /></div>
          </div>

          @if (insufficient()) {
            <div data-testid="transfer-insufficient-msg" class="sb-alert sb-alert-error" role="alert">Insufficient funds in {{ sourceAlias() }}.</div>
          }
          @if (transferType() !== 'Internal') {
            <div class="sb-alert sb-alert-error">Note: the Ledger API only settles <strong>Internal</strong> IBANs. {{ transferType() }} transfers to unknown IBANs will be rejected.</div>
          }

          <button data-testid="transfer-submit-btn" class="btn-primary" style="width:100%;padding:12px" [disabled]="submitDisabled()" (click)="openConfirm()">Review transfer</button>
        </div>
      </section>

      @if (confirmOpen()) {
        <div class="sb-modal-backdrop" (click)="confirmOpen.set(false)">
          <div data-testid="transfer-confirm-modal" role="dialog" aria-modal="true" class="sb-modal card" (click)="$event.stopPropagation()">
            <h3 style="font-size:18px;color:#fff;margin-bottom:4px">Confirm transfer</h3>
            <p class="muted" style="font-size:12px;margin-bottom:16px">Idempotency-protected booking</p>
            <div style="background:rgba(0,0,0,.25);border:1px solid rgba(255,255,255,.07);border-radius:10px;padding:14px;font-size:12.5px;display:flex;flex-direction:column;gap:8px">
              <div class="row-between"><span class="muted">From</span><strong data-testid="transfer-confirm-from" style="color:#fff">{{ sourceAlias() }}</strong></div>
              <div class="row-between"><span class="muted">To</span><span data-testid="transfer-confirm-to" class="mono" style="color:#fff">{{ formatIban(iban()) }}</span></div>
              <div class="row-between"><span class="muted">Amount</span><strong data-testid="transfer-confirm-amount" class="tnum" style="color:#fff">{{ amount() }} {{ sourceAccount()?.currency }}</strong></div>
              <div class="row-between"><span class="muted">Type</span><span style="color:#fff">{{ transferType() }}</span></div>
            </div>
            <div style="display:flex;gap:10px;margin-top:16px">
              <button data-testid="transfer-cancel-btn" class="btn-secondary" style="flex:1" (click)="confirmOpen.set(false)">Cancel</button>
              <button data-testid="transfer-confirm-btn" class="btn-primary" style="flex:1" (click)="confirm()" [disabled]="submitting()">{{ submitting() ? 'Booking…' : 'Confirm' }}</button>
            </div>
          </div>
        </div>
      }

      @if (successReference()) {
        <div data-testid="transfer-success-msg" class="sb-alert sb-alert-ok" role="status"><svg lucideCheck style="width:14px;height:14px" /> Transfer booked. Reference: <strong data-testid="transfer-reference" class="mono">{{ successReference() }}</strong></div>
      }
      @if (apiError()) {
        <div class="sb-alert sb-alert-error" role="alert">{{ apiError() }}</div>
      }
    </div>
  `,
})
export class TransferPage {
  private http = inject(HttpClient);
  accounts = signal<AccountSummary[]>([]);
  selectedSourceId = signal('');
  transferType = signal('Internal');
  iban = signal('');
  ibanStatus = signal<'unknown' | 'valid' | 'invalid' | 'verified' | 'unverified'>('unknown');
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
    const accounts = await firstValueFrom(
      this.http.get<AccountSummary[]>(`${environment.apiBaseUrl}/api/accounts`)
    );
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
    if (!raw) { this.ibanStatus.set('unknown'); return; }
    if (!isValidIban(raw)) { this.ibanStatus.set('invalid'); return; }
    try {
      const res = await firstValueFrom(
        this.http.get<any>(`${environment.apiBaseUrl}/api/accounts/iban/${compactIban(raw)}`)
      );
      if (res.verified) { this.ibanStatus.set('verified'); this.holderName.set(res.accountHolderName ?? ''); }
      else this.ibanStatus.set('unverified');
    } catch {
      this.ibanStatus.set('valid');
    }
  }

  openConfirm() {
    if (this.submitDisabled()) return;
    this.apiError.set(null);
    this.confirmOpen.set(true);
    setTimeout(() => (document.querySelector('[data-testid="transfer-confirm-btn"]') as HTMLElement | null)?.focus?.(), 0);
  }

  async confirm() {
    this.submitting.set(true);
    this.apiError.set(null);
    try {
      const key = crypto.randomUUID();
      const res = await firstValueFrom(
        this.http.post<any>(
          `${environment.apiBaseUrl}/api/transfers`,
          {
            sourceAccountId: this.selectedSourceId(),
            destinationIban: compactIban(this.iban()),
            amount: this.amount(),
            currency: this.sourceAccount()?.currency ?? 'EUR',
            transferType: this.transferType(),
            narrative: this.narrative() || undefined,
          },
          { headers: { 'Idempotency-Key': key } }
        )
      );
      this.confirmOpen.set(false);
      this.successReference.set(res.reference);
    } catch (e: any) {
      this.apiError.set(e.error?.detail ?? 'Transfer failed.');
    } finally {
      this.submitting.set(false);
    }
  }
}
