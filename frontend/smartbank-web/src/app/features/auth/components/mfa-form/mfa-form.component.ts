import { Component, input, output, viewChild } from '@angular/core';
import { OtpInputComponent } from '../../../../shared/ui/otp-input.component';
import { LucidePhone, LucideCopy, LucideCheck } from '@lucide/angular';

@Component({
  selector: 'sb-mfa-form',
  standalone: true,
  imports: [OtpInputComponent, LucidePhone, LucideCopy, LucideCheck],
  templateUrl: './mfa-form.component.html',
  styleUrl: './mfa-form.component.scss',
})
export class MfaFormComponent {
  email = input<string | null>(null);
  loading = input(false);
  error = input<string | null>(null);
  otpCode = input<string | null>(null);
  remaining = input('--:--');
  expired = input(false);
  canResend = input(false);
  justResent = input(false);
  copied = input(false);
  submitDisabled = input(true);

  codeChange = output<string>();
  submit = output<void>();
  resend = output<void>();
  fetchOtp = output<void>();
  copyOtp = output<void>();

  private otpInput = viewChild(OtpInputComponent);

  clearCode() {
    this.otpInput()?.setValue('');
  }
}
