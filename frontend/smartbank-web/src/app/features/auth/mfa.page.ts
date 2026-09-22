import { Component, inject, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { AuthStore } from './auth.store';
import { OtpInputComponent } from '../../shared/ui/otp-input.component';

@Component({
  selector: 'sb-mfa-form',
  standalone: true,
  imports: [OtpInputComponent],
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
          [disabled]="store.loading() || canResend() === false" (click)="resend()">Resend code</button>

        @if (expired()) {
          <div class="sb-alert sb-alert-error" style="margin-top:12px">Code expired — request a new one.</div>
        }
        @if (store.error()) {
          <div data-testid="mfa-error" role="alert" class="sb-alert sb-alert-error" style="margin-top:12px">{{ store.error() }}</div>
        }
      </div>
    </div>
  `,
})
export class MfaPage implements OnInit, OnDestroy {
  store = inject(AuthStore);
  code = signal('');
  now = signal(Date.now());
  private timer: ReturnType<typeof setInterval> | null = null;

  remaining = computed(() => {
    const exp = this.store.mfaExpiresAt() ?? Date.now();
    const ms = Math.max(0, exp - this.now());
    const s = Math.floor(ms / 1000);
    return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });
  expired = computed(() => (this.store.mfaExpiresAt() ?? 0) <= this.now());
  canResend = computed(() => {
    const exp = this.store.mfaExpiresAt() ?? 0;
    return exp - this.now() < 240_000;
  });

  ngOnInit() {
    this.timer = setInterval(() => this.now.set(Date.now()), 1000);
  }
  ngOnDestroy() {
    if (this.timer) clearInterval(this.timer);
  }
  submit() {
    if (this.code().length !== 6) return;
    this.store.verifyMfa(this.code());
  }
  resend() {
    this.store.resendMfa();
  }
}
