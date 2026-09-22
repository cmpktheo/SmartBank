import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'error';

@Injectable({ providedIn: 'root' })
export class ToastService {
  message = signal<string | null>(null);
  title = signal<string>('Done');
  type = signal<ToastType>('success');
  private timer: ReturnType<typeof setTimeout> | null = null;

  show(msg: string, title = 'Done', type: ToastType = 'success') {
    this.message.set(msg);
    this.title.set(title);
    this.type.set(type);
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => this.message.set(null), 4000);
  }
}
