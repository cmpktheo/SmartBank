import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { environment } from '../../../environments/environment';
import type { AccountSummary } from '../../core/models/models';

interface RecentTx {
  transactionId: string;
  reference: string;
  bookedAt: string;
  direction: string;
  amount: string;
  currency: string;
}

@Component({
  selector: 'sb-dashboard',
  standalone: true,
  imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `
    <div class="sb-container tab-pane">
      <!-- Balance overview: real accounts only, grouped by currency (no FX conversion API) -->
      <section class="card card-pad" aria-label="Account balances">
        <div class="card-head">
          <div style="display:flex;align-items:center;gap:12px">
            <div class="sb-brand-mark" aria-hidden="true">◈</div>
            <div>
              <h2 style="font-size:15px;color:#fff">Total balances</h2>
              <p class="muted" style="font-size:11.5px">Per-currency totals · no conversion applied</p>
            </div>
          </div>
          <span class="pill pill-blue"><span class="dot" style="width:6px;height:6px;border-radius:99px;background:#60a5fa"></span>{{ accounts().length }} accounts</span>
        </div>
        @if (loading()) {
          <p class="muted" style="font-size:13px">Loading accounts…</p>
        } @else if (accounts().length === 0) {
          <p class="muted" style="font-size:13px">No accounts yet. Open one to get started.</p>
        } @else {
          <div style="display:flex;flex-wrap:wrap;gap:12px;margin-bottom:16px">
            @for (g of totalsByCurrency(); track g.currency) {
              <div class="stat-mini" style="min-width:180px;flex:1">
                <span>Total in {{ g.currency }}</span>
                <strong class="tnum" style="font-size:20px">{{ +g.total | currency: g.currency }}</strong>
              </div>
            }
          </div>
          <div class="grid-3">
            @for (acc of accounts(); track acc.id) {
              <article data-testid="dashboard-account-card" [routerLink]="['/accounts', acc.id]" style="cursor:pointer"
                class="card-hover" tabindex="0" role="link" [attr.aria-label]="'Open ' + acc.alias">
                <div class="row-between" style="margin-bottom:12px">
                  <span class="micro-label" style="color:#93c5fd">{{ acc.type }} · {{ acc.currency }}</span>
                  <span class="pill" [class.pill-green]="acc.status==='Active'" [class.pill-red]="acc.status!=='Active'">
                    <span class="dot" [style.background]="acc.status==='Active' ? '#34d399' : '#f87171'"></span>{{ acc.status }}
                  </span>
                </div>
                <div data-testid="dashboard-account-alias" style="font-size:13px;font-weight:600;color:#fff">{{ acc.alias }}</div>
                <div data-testid="dashboard-account-iban" class="mono muted" style="font-size:11.5px;margin-top:2px">{{ acc.ibanFormatted }}</div>
                <div data-testid="dashboard-account-balance" class="tnum" style="font-size:24px;font-weight:700;color:#fff;margin-top:8px;font-family:Outfit,sans-serif">{{ +acc.availableBalance | currency: acc.currency }}</div>
                <div class="muted" style="font-size:11px;margin-top:4px">Available balance</div>
              </article>
            }
          </div>
        }
        <div class="divider" style="display:flex;gap:10px;flex-wrap:wrap">
          <a data-testid="quick-transfer-btn" routerLink="/transfers" class="btn-primary" style="flex:1;min-width:160px">↗ Transfer funds</a>
          <a data-testid="quick-cards-btn" routerLink="/cards" class="btn-secondary" style="flex:1;min-width:160px">▭ Manage cards</a>
        </div>
      </section>

      <!-- Recent activity: real GET /api/ledger/.../recent -->
      <section class="card card-pad" aria-label="Recent transactions">
        <div class="card-head">
          <div>
            <h2 style="font-size:16px;color:#fff">Recent transactions</h2>
            <p class="muted" style="font-size:12px">Real-time ledger · {{ recentAccountAlias() }}</p>
          </div>
          @if (firstAccountId()) {
            <a [routerLink]="['/accounts', firstAccountId()]" class="btn-secondary" style="font-size:12px;padding:8px 12px">View statement (CSV)</a>
          }
        </div>
        @if (recent().length === 0) {
          <p class="muted" style="font-size:12.5px;text-align:center;padding:24px">No recent activity.</p>
        } @else {
          <div class="sb-table-wrap">
            <table class="sb-table">
              <thead><tr><th>Transaction</th><th>Date</th><th style="text-align:right">Amount</th></tr></thead>
              <tbody>
                @for (tx of recent(); track tx.reference) {
                  <tr data-testid="recent-transaction-item">
                    <td><strong style="color:#fff;font-size:13px">{{ tx.reference }}</strong></td>
                    <td class="muted tnum">{{ tx.bookedAt | date:'medium' }}</td>
                    <td style="text-align:right" data-testid="recent-transaction-amount"
                      [class.amount-credit]="tx.direction==='Credit'" class="tnum mono">
                      {{ tx.direction === 'Credit' ? '+' : '−' }}{{ tx.amount }} {{ tx.currency }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        <p class="compliance">Protected by 256-bit TLS · Deposits insured up to $250,000</p>
      </section>
    </div>
  `,
  styles: [`
    article.card-hover { background: rgba(0,0,0,.25); border: 1px solid rgba(255,255,255,.07); border-radius: 12px; padding: 20px; }
  `],
})
export class DashboardPage implements OnInit {
  private http = inject(HttpClient);
  accounts = signal<AccountSummary[]>([]);
  recent = signal<RecentTx[]>([]);
  loading = signal(true);
  firstAccountId = computed(() => {
    const list = this.accounts();
    return (list.find((a) => a.type === 'Current') ?? list[0])?.id ?? '';
  });
  recentAccountAlias = computed(() => {
    const list = this.accounts();
    return (list.find((a) => a.type === 'Current') ?? list[0])?.alias ?? 'All accounts';
  });
  totalsByCurrency = computed(() => {
    const map = new Map<string, number>();
    for (const a of this.accounts()) map.set(a.currency, (map.get(a.currency) ?? 0) + Number(a.availableBalance));
    return [...map.entries()].map(([currency, total]) => ({ currency, total: String(total) }));
  });

  async ngOnInit() {
    try {
      const accounts = await firstValueFrom(
        this.http.get<AccountSummary[]>(`${environment.apiBaseUrl}/api/accounts`)
      );
      this.accounts.set(accounts);
      const first = accounts.find((a) => a.type === 'Current') ?? accounts[0];
      if (first) {
        const recent = await firstValueFrom(
          this.http.get<RecentTx[]>(`${environment.apiBaseUrl}/api/ledger/accounts/${first.id}/recent?limit=10`)
        );
        this.recent.set(recent);
      }
    } finally {
      this.loading.set(false);
    }
  }
}
