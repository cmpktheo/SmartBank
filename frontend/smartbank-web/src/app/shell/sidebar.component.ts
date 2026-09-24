import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { LucideLayoutGrid, LucideWallet, LucideArrowUpRight, LucideCreditCard, LucideSettings } from '@lucide/angular';

@Component({
  selector: 'sb-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, LucideLayoutGrid, LucideWallet, LucideArrowUpRight, LucideCreditCard, LucideSettings],
  template: `
    <aside class="sb-sidebar" [class.hidden-mobile]="!open()" aria-label="Primary navigation">
      <div class="sb-brand">
        <div class="sb-brand-mark" aria-hidden="true">S</div>
        <div class="min-w-0">
          <div class="sb-brand-name">SMART<span>BANK</span></div>
          <div class="micro-label text-[10px] mt-1">Enterprise Portal</div>
        </div>
</div>

      <nav class="sb-nav custom-scrollbar" aria-label="Sections">
        <div>
          <p class="micro-label sb-nav-group-label">Overview</p>
          <a data-testid="nav-dashboard" routerLink="/dashboard" routerLinkActive="active" class="sb-nav-link"><svg lucideLayoutGrid class="size-4" /> <span class="sb-nav-label">Dashboard</span></a>
          <a data-testid="nav-accounts" routerLink="/accounts" routerLinkActive="active" class="sb-nav-link"><svg lucideWallet class="size-4" /> <span class="sb-nav-label">Accounts</span></a>
        </div>
        <div>
          <p class="micro-label sb-nav-group-label">Money</p>
          <a data-testid="nav-transfers" routerLink="/transfers" routerLinkActive="active" class="sb-nav-link"><svg lucideArrowUpRight class="size-4" /> <span class="sb-nav-label">Transfers</span></a>
          <a data-testid="nav-cards" routerLink="/cards" routerLinkActive="active" class="sb-nav-link"><svg lucideCreditCard class="size-4" /> <span class="sb-nav-label">Cards</span></a>
        </div>
        <div>
          <p class="micro-label sb-nav-group-label">System</p>
          <a data-testid="nav-settings" routerLink="/settings" routerLinkActive="active" class="sb-nav-link"><svg lucideSettings class="size-4" /> <span class="sb-nav-label">Settings</span></a>
        </div>
      </nav>

      <div class="sb-secure">
        <div class="sb-secure-card">
          <p class="sb-secure-text">
            <span class="sb-secure-title">Secure session</span>&nbsp;
            <span class="tnum" data-testid="session-remaining">{{ remaining() }}</span> remaining
          </p>
          <button data-testid="nav-logout" (click)="logout.emit()" class="btn-secondary w-full mt-2.5 px-2 py-2 text-[12px]">Sign out</button>
        </div>
      </div>
    </aside>
  `,
})
export class SidebarComponent {
  open = input(false);
  logout = output<void>();
  remaining = input('--:--');
}
