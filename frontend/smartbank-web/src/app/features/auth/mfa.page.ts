import { Component, inject, signal, computed, OnInit, OnDestroy, viewChild } from '@angular/core';
import { AuthStore } from './auth.store';
import { OtpInputComponent } from '../../shared/ui/otp-input.component';
import { LucidePhone, LucideCopy, LucideCheck } from '@lucide/angular';

@Component({
  selector: 'sb-mfa-form',
  standalone: true,
  imports: [OtpInputComponent, LucidePhone, LucideCopy, LucideCheck],
  template: `
    <div class="sb-auth-wrap">
      <div class="sb-auth-card card card-pad tab-pane" style="text-align:center">
        <div class="sb-brand-mark" style="margin:0 auto 16px" aria-hidden="true">S</div>
        <h1 style="font-size:20px;color:#fff">Check your inbox</h1>
        <p class="muted" style="font-size:12.5px;margin:6px 0 20px">Enter the 6-digit code sent to <strong style="color:#e2e8f0">{{ store.email() ?? 'your email' }}</strong>.<br/>Boxes fill one by one — paste works too.</p>

        <sb-otp-input (codeChange)="code.set($event)" />

        <div data-testid="mfa-timer" class="tnum muted" style="font-size:12px;margin:14px 0">Code expires in {{ remaining() }}</div>

        <button data-testid="mfa-submit-btn" class="btn-primary" style="width:100%;padding:12px"
          [disabled]="code().length !== 6 || expired() || store.loading()" (click)="submit()">
          {{ store.loading() ? 'Verifying…' : 'Verify' }}</button>
        <button data-testid="mfa-resend-btn" class="btn-secondary" style="width:100%;margin-top:10px"
          [disabled]="store.loading() || !canResend()" (click)="resend()">
          {{ store.loading() ? 'Sending…' : 'Resend code' }}</button>

        @if (justResent()) {
          <div data-testid="mfa-resent-msg" role="status" class="sb-alert sb-alert-ok" style="margin-top:12px">New code sent — timer restarted. Check your inbox.</div>
        }

        @if (expired()) {
          <div class="sb-alert sb-alert-error" style="margin-top:12px">Code expired — request a new one.</div>
        }
        @if (store.error()) {
          <div data-testid="mfa-error" role="alert" class="sb-alert sb-alert-error" style="margin-top:12px">{{ store.error() }}</div>
        }

        @if (store.email()) {
          <div style="margin-top:16px;padding-top:12px;border-top:1px solid rgba(255,255,255,0.1)">
            <button data-testid="mfa-fetch-otp-btn" class="btn-secondary" style="padding:6px 12px;font-size:12px" (click)="fetchOtp()">
              Fetch OTP (Demo)
            </button>
          </div>
        }

        @if (store.otpCode()) {
          <div class="sb-alert sb-alert-ok" style="margin-top:12px;text-align:left">
            <div style="font-weight:700;font-size:13px;margin-bottom:4px;display:flex;align-items:center;gap:6px"><svg lucidePhone style="width:16px;height:16px;color:#6ee7b7" /> Simulated SMS</div>
            <div style="font-size:11px;color:#a78bfa;margin-bottom:6px">Sent to {{ store.email() }}</div>
            <div style="display:flex;align-items:center;gap:8px">
              <div data-testid="simulated-sms-otp" class="otp-display">{{ store.otpCode() }}</div>
              <button data-testid="mfa-copy-otp-btn" class="btn-icon" style="width:32px;height:32px;padding:0;display:inline-flex;align-items:center;justify-content:center;background:rgba(110,231,183,0.1);border:1px solid rgba(110,231,183,0.2);border-radius:8px;cursor:pointer;color:#6ee7b7" (click)="copyOtp()" [attr.aria-label]="copied() ? 'Copied' : 'Copy OTP'">
                @if (copied()) {
                  <svg lucideCheck data-testid="mfa-copy-otp-copied" style="width:16px;height:16px" />
                } @else {
                  <svg lucideCopy style="width:16px;height:16px" />
                }
              </button>
              @if (copied()) {
                <span data-testid="mfa-copied-msg" role="status" style="font-size:11px;color:#6ee7b7">Copied</span>
              }
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [
    `.otp-display {
      font-size: 22px;
      font-weight: 700;
      letter-spacing: 6px;
      color: #6ee7b7;
      font-family: monospace;
      margin-top: 4px;
    }`,
  ],
})
export class MfaPage implements OnInit, OnDestroy {
  store = inject(AuthStore);
  code = signal('');
  now = signal(Date.now());
  copied = signal(false);
  justResent = signal(false);
  otpInput = viewChild(OtpInputComponent);
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
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
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
      this.otpInput()?.setValue('');
      this.copied.set(false);
      this.justResent.set(true);
      if (this.resentTimeout) clearTimeout(this.resentTimeout);
      this.resentTimeout = setTimeout(() => this.justResent.set(false), 5000);
    } catch {
      // Error already surfaced via store.error() + interceptor toast.
    }
  }
}
