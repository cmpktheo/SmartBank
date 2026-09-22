import { Component, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthStore } from '../features/auth/auth.store';

@Component({
  selector: 'sb-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="sb-sidebar" [class.hidden-mobile]="!open()" aria-label="Primary navigation">
      <div class="sb-brand">
        <div class="sb-brand-mark" aria-hidden="true">S</div>
        <div style="min-width:0">
          <div class="sb-brand-name">SMART<span>BANK</span></div>
          <div class="micro-label" style="font-size:10px;margin-top:4px">Enterprise Portal</div>
        </div>
      </div>

      <div class="sb-client">
        <div class="sb-avatar" aria-hidden="true">{{ initials() }}</div>
        <div style="min-width:0;flex:1">
          <div style="font-size:13px;font-weight:600;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{{ displayEmail() }}</div>
        </div>
      </div>

      <nav class="sb-nav custom-scrollbar" aria-label="Sections">
        <div>
          <p class="micro-label sb-nav-group-label">Overview</p>
          <a data-testid="nav-dashboard" routerLink="/dashboard" routerLinkActive="active" class="sb-nav-link"><i class="sb-nav-ico">▦</i><span style="flex:1">Dashboard</span></a>
          <a data-testid="nav-accounts" routerLink="/accounts" routerLinkActive="active" class="sb-nav-link"><i class="sb-nav-ico">◈</i><span style="flex:1">Accounts</span></a>
        </div>
        <div>
          <p class="micro-label sb-nav-group-label">Money</p>
          <a data-testid="nav-transfers" routerLink="/transfers" routerLinkActive="active" class="sb-nav-link"><i class="sb-nav-ico">↗</i><span style="flex:1">Transfers</span></a>
          <a data-testid="nav-cards" routerLink="/cards" routerLinkActive="active" class="sb-nav-link"><i class="sb-nav-ico">▭</i><span style="flex:1">Cards</span></a>
        </div>
        <div>
          <p class="micro-label sb-nav-group-label">System</p>
          <a data-testid="nav-settings" routerLink="/settings" routerLinkActive="active" class="sb-nav-link"><i class="sb-nav-ico">⚙</i><span style="flex:1">Settings</span></a>
        </div>
      </nav>

      <div class="sb-secure">
        <div class="sb-secure-card">
          <p style="font-size:11px;color:var(--muted);margin-top:6px;line-height:1.5">
            <span style="font-size:12px;font-weight:600;color:#fff">Secure session</span>
            <span class="tnum" data-testid="session-remaining">{{ remaining() }}</span> remaining
          </p>
          <button data-testid="nav-logout" (click)="logout.emit()" class="btn-secondary" style="width:100%;margin-top:10px;padding:8px;font-size:12px">Sign out</button>
        </div>
        <p class="sb-footnote" style="margin-top:10px">FDIC insured · v4.2.1</p>
      </div>
    </aside>
  `,
})
export class SidebarComponent {
  store = inject(AuthStore);
  open = input(false);
  logout = output<void>();
  remaining = input('--:--');

  displayEmail(): string {
    return this.store.email() ?? 'Customer';
  }
  initials(): string {
    const e = this.store.email() ?? 'C';
    return e.slice(0, 2).toUpperCase();
  }
}
