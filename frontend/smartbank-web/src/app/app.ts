import { Component, effect, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AuthStore } from './features/auth/auth.store';
import { ToastService } from './shared/ui/toast';
import { SidebarComponent } from './shell/sidebar.component';
import { TopbarComponent } from './shell/topbar.component';
import { ToastComponent } from './shared/ui/toast/toast.component';

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
  imports: [RouterOutlet, SidebarComponent, TopbarComponent, ToastComponent],
  template: `
    @if (isAuthRoute()) {
      <router-outlet />
    } @else {
      <div class="sb-app">
        @if (sidebarOpen()) {
          <div class="sb-scrim" (click)="sidebarOpen.set(false)"></div>
        }
        <sb-sidebar [open]="sidebarOpen()" [remaining]="remaining()" (logout)="logout()" />
        <div class="sb-main">
          <sb-topbar
            [title]="pageTitle()"
            [crumb]="pageCrumb()"
            [subtitle]="subtitle()"
            [email]="userEmail()"
            [initials]="userInitials()"
            (menu)="sidebarOpen.set(!sidebarOpen())"
          />
          <main class="sb-viewport custom-scrollbar"><router-outlet /></main>
        </div>
      </div>
    }
    <sb-toast [message]="toast.message()" [title]="toast.title()" [type]="toast.type()" />
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
  userEmail = computed(() => this.store.email() ?? 'Customer');
  userInitials = computed(() => (this.store.email() ?? 'C').slice(0, 2).toUpperCase());
  pageTitle = computed(() => {
    const base = TITLES[this.basePath()]?.title ?? 'SmartBank Portal';
    if (this.basePath() === '/dashboard' && this.store.email())
      return `Welcome back, ${this.store.email()!.split('@')[0]}`;
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

  async ngOnInit() {
    this.store.hydrateFromSession();
    // A token in sessionStorage proves nothing: after the backend is wiped the
    // old JWT still verifies (static dev key) while its user no longer exists.
    // Re-check with the backend before letting the guard trust the local token.
    if (this.store.accessToken()) await this.store.validateSession();
    this.routePath.set(this.router.url.split('?')[0]);
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe((e: unknown) => {
      const nav = e as { urlAfterRedirects?: string; url?: string };
      this.routePath.set((nav.urlAfterRedirects ?? nav.url ?? '').split('?')[0]);
      this.sidebarOpen.set(false);
    });
    this.timer = setInterval(() => {
      this.now.set(Date.now());
      // Keep the session alive while the tab is open: when the token nears
      // expiry, rotate it in the background. setTokens() bumps expiresAt, so
      // the sidebar countdown and subtitle automatically refresh. No HTTP
      // happens unless needsRefresh() is true, and concurrent ticks/actions
      // share one refresh call.
      void this.store.refreshIfNeeded().catch(() => {});
    }, 1000);
  }
  ngOnDestroy() {
    if (this.timer) clearInterval(this.timer);
  }
  logout() {
    this.store.logout();
  }
}
