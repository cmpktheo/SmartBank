import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private http = inject(HttpClient);
  private router = inject(Router);

  accessToken = signal<string | null>(null);
  refreshToken = signal<string | null>(null);
  expiresAt = signal<number | null>(null);
  customerId = signal<string | null>(null);
  email = signal<string | null>(null);
  mfaChallengeId = signal<string | null>(null);
  mfaExpiresAt = signal<number | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);
  otpCode = signal<string | null>(null);

  isAuthenticated = computed(() => !!this.accessToken());
  private refreshing: Promise<void> | null = null;
  /** Refresh proactively this far ahead of expiry so user actions extend the session. */
  readonly refreshSkewMs = 60_000;

  /** True when we hold a token that will expire within the skew window (or already expired). */
  needsRefresh(now = Date.now()): boolean {
    const token = this.accessToken();
    const exp = this.expiresAt();
    if (!token || exp === null) return false;
    return exp - now <= this.refreshSkewMs;
  }

  /**
   * Refresh the session if the access token is expiring soon.
   * Returns true when the session is usable afterwards (already fresh or refreshed),
   * false when there is no session or the refresh failed (session cleared).
   * Concurrent callers share a single backend call via `refreshing`.
   */
  async refreshIfNeeded(now = Date.now()): Promise<boolean> {
    if (!this.accessToken()) return false;
    if (!this.needsRefresh(now)) return true;
    await this.refresh();
    return !!this.accessToken();
  }

  async login(email: string, password: string) {
    this.loading.set(true);
    this.error.set(null);
    try {
      const res = await firstValueFrom(
        this.http.post<any>(`${environment.apiBaseUrl}/api/auth/login`, { email, password })
      );
      if (res.mfaRequired) {
        this.mfaChallengeId.set(res.challengeId);
        this.mfaExpiresAt.set(Date.now() + res.expiresInSeconds * 1000);
        this.email.set(email);
        this.otpCode.set(null);
        this.router.navigate(['/auth/mfa']);
      } else {
        this.setTokens(res);
        this.router.navigate(['/dashboard']);
      }
    } catch (e: any) {
      this.error.set(e.error?.detail ?? 'Login failed.');
    } finally {
      this.loading.set(false);
    }
  }

  async verifyMfa(code: string) {
    this.loading.set(true);
    this.error.set(null);
    try {
      const res = await firstValueFrom(
        this.http.post<any>(`${environment.apiBaseUrl}/api/auth/mfa/verify`, {
          challengeId: this.mfaChallengeId(),
          code,
        })
      );
      this.setTokens(res);
      this.router.navigate(['/dashboard']);
    } catch (e: any) {
      this.error.set(e.error?.detail ?? 'Invalid code.');
    } finally {
      this.loading.set(false);
    }
  }

  async resendMfa() {
    this.loading.set(true);
    this.error.set(null);
    try {
      const res = await firstValueFrom(
        this.http.post<{ expiresInSeconds: number }>(`${environment.apiBaseUrl}/api/auth/mfa/resend`, {
          challengeId: this.mfaChallengeId(),
          email: this.email(),
        })
      );
      const expiresIn = res?.expiresInSeconds ?? 60;
      this.mfaExpiresAt.set(Date.now() + expiresIn * 1000);
      // Old demo code is now invalid — force a fresh fetch instead of showing a stale OTP.
      this.otpCode.set(null);
    } catch (e: any) {
      this.error.set(e.error?.detail ?? e.error?.title ?? 'Could not resend code. Try again.');
      throw e;
    } finally {
      this.loading.set(false);
    }
  }

  async fetchOtp() {
    const email = this.email();
    if (!email) return null;
    try {
      const res = await firstValueFrom(
        this.http.get<{ code: string }>(`${environment.apiBaseUrl}/api/auth/e2e/otp?email=${email}`)
      );
      this.otpCode.set(res.code ?? null);
      return res.code ?? null;
    } catch {
      this.otpCode.set(null);
      return null;
    }
  }

  async refresh(): Promise<void> {
    if (this.refreshing) return this.refreshing;
    this.refreshing = (async () => {
      try {
        const res = await firstValueFrom(
          this.http.post<any>(`${environment.apiBaseUrl}/api/auth/refresh`, {
            refreshToken: this.refreshToken(),
          })
        );
        this.setTokens(res);
      } catch {
        await this.logout();
      } finally {
        this.refreshing = null;
      }
    })();
    return this.refreshing;
  }

  async logout() {
    try {
      await firstValueFrom(this.http.post(`${environment.apiBaseUrl}/api/auth/logout`, {}));
    } catch {
      /* ignore */
    }
    await this.clearSession();
  }

  /** Drop the local session without calling the backend (token already dead/invalid). */
  async clearSession() {
    this.accessToken.set(null);
    this.refreshToken.set(null);
    this.expiresAt.set(null);
    this.customerId.set(null);
    sessionStorage.removeItem('sb.tokens');
    sessionStorage.removeItem('sb.selectedAccountId');
    await this.router.navigate(['/auth/login'], { replaceUrl: true });
  }

  /**
   * Re-validate a hydrated session against the backend. Access tokens are
   * stateless (signature + expiry only) and the dev signing key is static, so a
   * token issued before the backend was wiped still verifies. /api/auth/me also
   * looks the user up in the Identity DB, so it 401s when the user is gone.
   * Only 401/403 drops the session — any other failure (backend down) keeps it
   * so a retry can succeed.
   */
  async validateSession(): Promise<boolean> {
    if (!this.accessToken()) return false;
    try {
      const me = await firstValueFrom(
        this.http.get<{ email?: string; customerId?: string }>(`${environment.apiBaseUrl}/api/auth/me`)
      );
      if (me?.email) this.email.set(me.email);
      if (me?.customerId) this.customerId.set(me.customerId);
      return true;
    } catch (e: unknown) {
      const status = (e as { status?: number })?.status;
      if (status === 401 || status === 403) {
        await this.clearSession();
        return false;
      }
      return true;
    }
  }

  hydrateFromSession() {
    const raw = sessionStorage.getItem('sb.tokens');
    if (!raw) return;
    try {
      const t = JSON.parse(raw);
      this.accessToken.set(t.accessToken ?? null);
      this.refreshToken.set(t.refreshToken ?? null);
      this.expiresAt.set(t.expiresAt ?? null);
      this.customerId.set(t.customerId ?? null);
      this.email.set(t.email ?? null);
    } catch {
      /* ignore */
    }
  }

  private setTokens(res: any) {
    this.accessToken.set(res.accessToken);
    this.refreshToken.set(res.refreshToken);
    this.expiresAt.set(Date.now() + res.expiresIn * 1000);
    this.customerId.set(res.customerId ?? null);
    this.mfaChallengeId.set(null);
    sessionStorage.setItem(
      'sb.tokens',
      JSON.stringify({
        accessToken: res.accessToken,
        refreshToken: res.refreshToken,
        expiresAt: this.expiresAt(),
        customerId: res.customerId,
        email: this.email(),
      })
    );
  }
}
