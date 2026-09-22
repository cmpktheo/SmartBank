import { Component, effect, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AuthStore } from './features/auth/auth.store';
import { ToastService } from './shared/ui/toast';
import { SidebarComponent } from './shell/sidebar.component';
import { TopbarComponent } from './shell/topbar.component';

const TITLES: Record<string, { title: string; crumb: string }> = {
  '/dashboard': { title: 'Welcome back', crumb: 'Dashboard' },
  '/accounts': { title: 'Accounts', crumb: 'Accounts' },
  '/transfers': { title: 'Transfers', crumb: 'Transfers' },
  '/cards': { title: 'Cards', crumb: 'Cards' },
  '/settings': { title: 'Settings', crumb: 'Settings' },
};

@Component({
  selector: 'sb-root',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, TopbarComponent],
  template: `
    @if (isAuthRoute()) {
      <router-outlet />
    } @else {
      <div class="sb-app">
        @if (sidebarOpen()) { <div class="sb-scrim" (click)="sidebarOpen.set(false)"></div> }
        <sb-sidebar [open]="sidebarOpen()" [remaining]="remaining()" (logout)="logout()" />
        <div class="sb-main">
          <sb-topbar [title]="pageTitle()" [crumb]="pageCrumb()" [subtitle]="subtitle()" (menu)="sidebarOpen.set(!sidebarOpen())" />
          <main class="sb-viewport custom-scrollbar"><router-outlet /></main>
        </div>
      </div>
    }
    @if (toast.message()) {
      <div data-testid="toast" role="status" class="sb-toast card">
        <div style="padding:14px 16px;display:flex;gap:12px;align-items:flex-start">
          <span style="width:32px;height:32px;border-radius:8px;display:flex;align-items:center;justify-content:center;flex-shrink:0"
            [style.background]="toast.type() === 'error' ? 'rgba(248,113,113,.15)' : 'rgba(59,130,246,.15)'"
            [style.color]="toast.type() === 'error' ? '#fca5a5' : '#93c5fd'">{{ toast.type() === 'error' ? '!' : '✓' }}</span>
          <div style="min-width:0">
            <p style="font-size:12.5px;font-weight:600;color:#fff">{{ toast.title() }}</p>
            <p style="font-size:12px;color:var(--muted);margin-top:2px">{{ toast.message() }}</p>
          </div>
        </div>
        <div class="sb-toast-bar"><div></div></div>
      </div>
    }
  `,
})
export class App implements OnInit, OnDestroy {
  store = inject(AuthStore);
  toast = inject(ToastService);
  private router = inject(Router);
  sidebarOpen = signal(false);
  routePath = signal('/dashboard');
  now = signal(Date.now());
  private timer: ReturnType<typeof setInterval> | null = null;

  // When the session timer hits 0, end the session and replace history
  // with the login page so Back cannot return to the protected screen.
  private expireOnTimeout = effect(() => {
    const exp = this.store.expiresAt();
    const now = this.now();
    if (exp !== null && now >= exp && this.store.accessToken()) {
      this.toast.show('Your session has expired. Please sign in again.', 'Session expired', 'error');
      this.logout();
    }
  });

  isAuthRoute = computed(() => this.routePath().startsWith('/auth'));
  pageTitle = computed(() => {
    const base = TITLES[this.basePath()]?.title ?? 'SmartBank Portal';
    if (this.basePath() === '/dashboard' && this.store.email()) return `Welcome back, ${this.store.email()!.split('@')[0]}`;
    return base;
  });
  pageCrumb = computed(() => TITLES[this.basePath()]?.crumb ?? 'Portal');
  subtitle = computed(() => {
    const exp = this.store.expiresAt();
    if (!exp) return '';
    return `Session ends ${new Date(exp).toLocaleTimeString()}`;
  });
  remaining = computed(() => {
    const exp = this.store.expiresAt();
    if (!exp) return '--:--';
    const ms = Math.max(0, exp - this.now());
    const s = Math.floor(ms / 1000);
    return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });

  private basePath(): string {
    const p = this.routePath();
    if (p.startsWith('/accounts')) return '/accounts';
    return p;
  }

  ngOnInit() {
    this.store.hydrateFromSession();
    this.routePath.set(this.router.url.split('?')[0]);
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe((e: any) => {
      this.routePath.set((e.urlAfterRedirects ?? e.url).split('?')[0]);
      this.sidebarOpen.set(false);
    });
    this.timer = setInterval(() => this.now.set(Date.now()), 1000);
  }
  ngOnDestroy() {
    if (this.timer) clearInterval(this.timer);
  }
  logout() {
    this.store.logout();
  }
}
