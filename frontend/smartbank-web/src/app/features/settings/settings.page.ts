import { Component, inject, computed } from '@angular/core';
import { AuthStore } from '../auth/auth.store';

@Component({
  selector: 'sb-settings',
  standalone: true,
  template: `
    <div class="sb-container tab-pane" style="max-width:720px">
      <div><h2 style="font-size:22px;color:#fff">Settings</h2>
        <p class="muted" style="font-size:12.5px">Only real session data — no mock preferences</p></div>
      <section class="card card-pad">
        <h3 style="font-size:15px;color:#fff;margin-bottom:12px">Session &amp; security</h3>
        <div style="font-size:13px;display:flex;flex-direction:column;gap:0">
          <div class="row-between" style="padding:14px 0;border-top:1px solid rgba(255,255,255,.06)">
            <div><strong style="color:#fff">Signed in as</strong><p class="muted" style="font-size:12px">{{ store.email() ?? '—' }}</p></div>
            <span class="pill pill-green"><span class="dot" style="width:6px;height:6px;border-radius:99px;background:#34d399"></span>Verified</span>
          </div>
          <div class="row-between" style="padding:14px 0;border-top:1px solid rgba(255,255,255,.06)">
            <div><strong style="color:#fff">Customer ID</strong><p class="mono muted" style="font-size:12px">{{ store.customerId() ?? '—' }}</p></div>
          </div>
          <div class="row-between" style="padding:14px 0;border-top:1px solid rgba(255,255,255,.06)">
            <div><strong style="color:#fff">Session expires</strong><p class="muted tnum" style="font-size:12px">{{ expiresText() }}</p></div>
            <span class="pill pill-blue">256-bit TLS</span>
          </div>
          <div class="row-between" style="padding:14px 0;border-top:1px solid rgba(255,255,255,.06)">
            <div><strong style="color:#fff">MFA</strong><p class="muted" style="font-size:12px">Required at every login (email code)</p></div>
            <span class="pill pill-green">Enforced</span>
          </div>
        </div>
        <div class="divider">
          <button data-testid="nav-logout" class="btn-secondary" style="width:100%" (click)="store.logout()">Sign out</button>
        </div>
      </section>
      <p class="compliance">Appearance: high-contrast dark navy, OLED optimized · No analytics or marketing toggles — not offered by the API.</p>
    </div>
  `,
})
export class SettingsPage {
  store = inject(AuthStore);
  expiresText = computed(() => {
    const exp = this.store.expiresAt();
    return exp ? new Date(exp).toLocaleString() : '—';
  });
}
