import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideMenu, LucideArrowUpRight, LucideChevronRight } from '@lucide/angular';

@Component({
  selector: 'sb-topbar',
  standalone: true,
  imports: [RouterLink, LucideMenu, LucideArrowUpRight, LucideChevronRight],
  template: `
    <header class="sb-topbar">
      <button (click)="menu.emit()" class="btn-secondary btn-icon sb-nav-btn shrink-0" aria-label="Open navigation"><svg lucideMenu class="size-[18px]" /></button>
      <div class="sb-topbar-inner">
        <nav class="sb-crumb" aria-label="Breadcrumb"><span>Private Banking</span><svg lucideChevronRight class="size-3" /><span class="sb-crumb-current">{{ crumb() }}</span></nav>
        <h1 class="sb-title">{{ title() }}</h1>
        <p class="sb-subtitle">{{ subtitle() }}</p>
      </div>
      <div class="sb-top-actions">
        <a data-testid="topbar-transfer-btn" routerLink="/transfers" class="btn-primary"><svg lucideArrowUpRight class="size-4" /> <span>Send money</span></a>
        <div class="sb-user-chip">
          <div class="sb-avatar" aria-hidden="true">{{ initials() }}</div>
          <div class="leading-tight">
            <div class="sb-user-email">{{ email() }}</div>
          </div>
        </div>
      </div>
    </header>
  `,
})
export class TopbarComponent {
  title = input('Dashboard');
  crumb = input('Dashboard');
  subtitle = input('');
  email = input('Customer');
  initials = input('C');
  menu = output<void>();
}
