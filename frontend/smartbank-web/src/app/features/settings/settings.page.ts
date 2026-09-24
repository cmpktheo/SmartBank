import { Component, inject, computed } from '@angular/core';
import { AuthStore } from '../auth/auth.store';
import { SessionCardComponent } from './components/session-card/session-card.component';

@Component({
  selector: 'sb-settings',
  standalone: true,
  imports: [SessionCardComponent],
  template: `
    <div class="sb-container sb-narrow tab-pane">
      <div>
        <h2 class="sb-page-title">Settings</h2>
        <p class="muted text-[12.5px]">Only real session data — no mock preferences</p>
      </div>
      <sb-session-card
        [email]="store.email() ?? '—'"
        [customerId]="store.customerId() ?? '—'"
        [expiresText]="expiresText()"
        (signOut)="store.logout()"
      />
      <p class="compliance">
        Appearance: high-contrast dark navy, OLED optimized · No analytics or marketing toggles —
        not offered by the API.
      </p>
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
