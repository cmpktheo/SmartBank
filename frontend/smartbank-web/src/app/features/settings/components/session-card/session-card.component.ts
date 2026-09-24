import { Component, input, output } from '@angular/core';

@Component({
  selector: 'sb-session-card',
  standalone: true,
  templateUrl: './session-card.component.html',
  styleUrl: './session-card.component.scss',
})
export class SessionCardComponent {
  email = input('—');
  customerId = input('—');
  expiresText = input('—');
  signOut = output<void>();
}
