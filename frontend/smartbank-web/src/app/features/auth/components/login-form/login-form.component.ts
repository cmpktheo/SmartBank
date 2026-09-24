import { Component, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

export interface LoginCredentials {
  email: string;
  password: string;
}

@Component({
  selector: 'sb-login-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login-form.component.html',
  styleUrl: './login-form.component.scss',
})
export class LoginFormComponent {
  loading = input(false);
  error = input<string | null>(null);
  login = output<LoginCredentials>();

  private fb = inject(FormBuilder);
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  submit() {
    if (this.form.invalid) return;
    this.login.emit({ email: this.form.value.email!, password: this.form.value.password! });
  }
}
