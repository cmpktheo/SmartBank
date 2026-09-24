import { Component, input } from '@angular/core';
import { LucideAlertCircle, LucideCheck } from '@lucide/angular';
import type { ToastType } from '../toast';

@Component({
  selector: 'sb-toast',
  standalone: true,
  imports: [LucideAlertCircle, LucideCheck],
  templateUrl: './toast.component.html',
  styleUrl: './toast.component.scss',
})
export class ToastComponent {
  message = input<string | null>(null);
  title = input('Done');
  type = input<ToastType>('success');
}
