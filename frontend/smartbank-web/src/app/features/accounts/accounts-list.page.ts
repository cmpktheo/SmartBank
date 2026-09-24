import { Component, inject, signal, OnInit, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AccountSelectionStore } from './account-selection.store';
import { AccountsService } from './data-access/accounts.service';
import type { AccountSummary } from '../../core/models/models';
import {
  AccountCreateFormComponent,
  type CreateAccountPayload,
} from './components/account-create-form/account-create-form.component';
import { AccountGridComponent } from './components/account-grid/account-grid.component';

@Component({
  selector: 'sb-accounts-list',
  standalone: true,
  imports: [AccountCreateFormComponent, AccountGridComponent],
  template: `
    <div class="sb-container tab-pane">
      <div class="row-between flex-wrap">
        <div>
          <h2 class="sb-page-title">Accounts</h2>
          <p class="muted text-[12.5px]">Your IBAN accounts · {{ accounts().length }} total</p>
        </div>
        <button class="btn-primary text-[12.5px]" (click)="showNew.set(!showNew())">
          + Open new account
        </button>
      </div>

      @if (showNew()) {
        <sb-account-create-form
          [creating]="creating()"
          [error]="error()"
          (create)="openAccount($event)"
        />
      }

      <sb-account-grid [accounts]="accounts()" (openAccount)="openDetail($event)" />
    </div>
  `,
})
export class AccountsListPage implements OnInit {
  private accountsService = inject(AccountsService);
  private router = inject(Router);
  private selection = inject(AccountSelectionStore);
  private createForm = viewChild(AccountCreateFormComponent);

  accounts = signal<AccountSummary[]>([]);
  showNew = signal(false);
  creating = signal(false);
  error = signal<string | null>(null);

  async ngOnInit() {
    const accounts = await firstValueFrom(this.accountsService.getAccounts());
    this.accounts.set(accounts);
  }

  openDetail(id: string) {
    this.selection.select(id);
    this.router.navigate(['/accounts/detail']);
  }

  async openAccount(payload: CreateAccountPayload) {
    this.creating.set(true);
    this.error.set(null);
    try {
      const created = await firstValueFrom(
        this.accountsService.openAccount(payload.alias, payload.type, payload.currency)
      );
      this.accounts.update((l) => [...l, created]);
      this.showNew.set(false);
      this.createForm()?.reset();
    } catch (e: unknown) {
      const detail = (e as { error?: { detail?: string } })?.error?.detail;
      this.error.set(detail ?? 'Could not open account.');
    } finally {
      this.creating.set(false);
    }
  }
}
