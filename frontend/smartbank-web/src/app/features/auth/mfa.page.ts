import { Component, inject, signal, computed, OnInit, OnDestroy, viewChild } from '@angular/core';
import { AuthStore } from './auth.store';
import { MfaFormComponent } from './components/mfa-form/mfa-form.component';

@Component({
  selector: 'sb-mfa-page',
  standalone: true,
  imports: [MfaFormComponent],
  template: `
    <sb-mfa-form
      [email]="store.email()"
      [loading]="store.loading()"
      [error]="store.error()"
      [otpCode]="store.otpCode()"
      [remaining]="remaining()"
      [expired]="expired()"
      [canResend]="canResend()"
      [justResent]="justResent()"
      [copied]="copied()"
      [submitDisabled]="code().length !== 6 || expired() || store.loading()"
      (codeChange)="code.set($event)"
      (submit)="submit()"
      (resend)="resend()"
      (fetchOtp)="fetchOtp()"
      (copyOtp)="copyOtp()"
    />
  `,
})
export class MfaPage implements OnInit, OnDestroy {
  store = inject(AuthStore);
  code = signal('');
  now = signal(Date.now());
  copied = signal(false);
  justResent = signal(false);
  otpInput = viewChild(MfaFormComponent);
  private timer: ReturnType<typeof setInterval> | null = null;
  private copiedTimeout: ReturnType<typeof setTimeout> | null = null;
  private resentTimeout: ReturnType<typeof setTimeout> | null = null;

  async fetchOtp() {
    this.copied.set(false);
    await this.store.fetchOtp();
  }

  async copyOtp() {
    const otp = this.store.otpCode();
    if (!otp) return;
    try {
      await navigator.clipboard.writeText(otp);
    } catch {
      // Fallback for non-secure contexts: select + execCommand
      try {
        const ta = document.createElement('textarea');
        ta.value = otp;
        ta.classList.add('sb-clipboard-hidden');
        document.body.appendChild(ta);
        ta.select();
        document.execCommand('copy');
        ta.remove();
      } catch {
        return;
      }
    }
    this.copied.set(true);
    if (this.copiedTimeout) clearTimeout(this.copiedTimeout);
    this.copiedTimeout = setTimeout(() => this.copied.set(false), 1500);
  }

  remaining = computed(() => {
    const exp = this.store.mfaExpiresAt() ?? Date.now();
    const ms = Math.max(0, exp - this.now());
    const s = Math.floor(ms / 1000);
    return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });
  expired = computed(() => (this.store.mfaExpiresAt() ?? 0) <= this.now());
  // Resend is only allowed once the current code has expired.
  canResend = computed(() => this.expired());

  ngOnInit() {
    this.timer = setInterval(() => this.now.set(Date.now()), 1000);
  }
  ngOnDestroy() {
    if (this.timer) clearInterval(this.timer);
    if (this.copiedTimeout) clearTimeout(this.copiedTimeout);
    if (this.resentTimeout) clearTimeout(this.resentTimeout);
  }
  submit() {
    if (this.code().length !== 6) return;
    this.store.verifyMfa(this.code());
  }
  async resend() {
    if (this.store.loading() || !this.canResend()) return;
    this.justResent.set(false);
    try {
      await this.store.resendMfa();
      // New server code invalidates what the user typed + the stale demo OTP.
      this.code.set('');
      this.otpInput()?.clearCode();
      this.copied.set(false);
      this.justResent.set(true);
      if (this.resentTimeout) clearTimeout(this.resentTimeout);
      this.resentTimeout = setTimeout(() => this.justResent.set(false), 5000);
    } catch {
      // Error already surfaced via store.error() + interceptor toast.
    }
  }
}
