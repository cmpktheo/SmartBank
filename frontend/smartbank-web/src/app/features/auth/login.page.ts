import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from './auth.store';

@Component({
  selector: 'sb-login-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="sb-auth-wrap">
      <div class="sb-auth-card card card-pad tab-pane">
        <div style="display:flex;align-items:center;gap:12px;margin-bottom:20px">
          <div class="sb-brand-mark" aria-hidden="true">S</div>
          <div><div style="font-size:17px;font-weight:700;font-family:Outfit,sans-serif">SMART<span style="color:#60a5fa">BANK</span></div>
          <div class="micro-label" style="font-size:10px">Enterprise Portal</div></div>
        </div>
        <h1 style="font-size:20px;color:#fff">Sign in</h1>
        <p class="muted" style="font-size:12.5px;margin:4px 0 20px">Password first — we'll send a 6-digit code next.</p>
        <form [formGroup]="form" (ngSubmit)="submit()" class="sb-form-row">
          <div><label class="sb-label" for="email">Email</label>
            <input id="email" data-testid="login-email-input" type="email" autocomplete="username" formControlName="email" class="field" placeholder="you@smartbank.test" /></div>
          <div><label class="sb-label" for="password">Password</label>
            <input id="password" data-testid="login-password-input" type="password" autocomplete="current-password" formControlName="password" class="field" placeholder="••••••••" /></div>
          <button data-testid="login-submit-btn" type="submit" class="btn-primary" style="width:100%;padding:12px" [disabled]="form.invalid || store.loading()">
            {{ store.loading() ? 'Signing in…' : 'Sign in' }}</button>
          @if (store.error()) {
            <div data-testid="login-error" role="alert" class="sb-alert sb-alert-error">{{ store.error() }}</div>
          }
        </form>
        <p class="muted" style="font-size:11px;text-align:center;margin-top:16px">Protected by 256-bit TLS · SOC 2 Type II</p>
      </div>
    </div>
  `,
})
export class LoginPage {
  private fb = inject(FormBuilder);
  store = inject(AuthStore);
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });
  submit() {
    if (this.form.invalid) return;
    this.store.login(this.form.value.email!, this.form.value.password!);
  }
}
