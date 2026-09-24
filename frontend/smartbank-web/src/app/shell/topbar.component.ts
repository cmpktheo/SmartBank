import { Component, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../features/auth/auth.store';
import { LucideMenu, LucideArrowUpRight, LucideChevronRight } from '@lucide/angular';

@Component({
  selector: 'sb-topbar',
  standalone: true,
  imports: [RouterLink, LucideMenu, LucideArrowUpRight, LucideChevronRight],
  template: `
    <header class="sb-topbar">
      <button (click)="menu.emit()" class="btn-secondary btn-icon sb-nav-btn" aria-label="Open navigation" style="flex-shrink:0"><svg lucideMenu style="width:18px;height:18px" /></button>
      <div style="min-width:0;flex:1">
        <nav class="sb-crumb" aria-label="Breadcrumb"><span>Private Banking</span><svg lucideChevronRight style="width:12px;height:12px" /><span style="color:#cbd5e1">{{ crumb() }}</span></nav>
        <h1 class="sb-title">{{ title() }}</h1>
        <p class="sb-subtitle">{{ subtitle() }}</p>
      </div>
      <div class="sb-top-actions">
        <a data-testid="quick-transfer-btn" routerLink="/transfers" class="btn-primary"><svg lucideArrowUpRight style="width:16px;height:16px" /> <span>Send money</span></a>
        <div class="sb-user-chip">
          <div class="sb-avatar" style="width:36px;height:36px" aria-hidden="true">{{ initials() }}</div>
          <div style="line-height:1.2">
            <div style="font-size:12.5px;font-weight:600;color:#fff">{{ email() }}</div>
          </div>
        </div>
      </div>
    </header>
  `,
})
export class TopbarComponent {
  store = inject(AuthStore);
  title = input('Dashboard');
  crumb = input('Dashboard');
  subtitle = input('');
  menu = output<void>();
  email() { return this.store.email() ?? 'Customer'; }
  initials() { return (this.store.email() ?? 'C').slice(0, 2).toUpperCase(); }
}
