import { Component, inject } from '@angular/core';
import {
  LoginFormComponent,
  type LoginCredentials,
} from './components/login-form/login-form.component';
import { AuthStore } from './auth.store';

@Component({
  selector: 'sb-login-page',
  standalone: true,
  imports: [LoginFormComponent],
  template: `
    <sb-login-form
      [loading]="store.loading()"
      [error]="store.error()"
      (login)="submit($event)"
    />
  `,
})
export class LoginPage {
  store = inject(AuthStore);

  submit(credentials: LoginCredentials) {
    this.store.login(credentials.email, credentials.password);
  }
}
