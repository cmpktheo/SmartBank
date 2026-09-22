import { Component, input, output, signal, effect, viewChildren, ElementRef, afterNextRender } from '@angular/core';

/**
 * 6-box OTP input. First box keeps data-testid="mfa-code-input" so the
 * existing Playwright `fill()` flow keeps working (full string is split
 * across boxes). Other boxes: mfa-code-input-1..5.
 */
@Component({
  selector: 'sb-otp-input',
  standalone: true,
  template: `
    <div class="sb-otp" role="group" aria-label="6-digit verification code">
      @for (b of boxes(); track $index) {
        <input
          #otpBox
          [attr.data-testid]="$index === 0 ? 'mfa-code-input' : 'mfa-code-input-' + $index"
          [attr.aria-label]="'Digit ' + ($index + 1)"
          inputmode="numeric"
          autocomplete="one-time-code"
          maxlength="6"
          [value]="b"
          [class.filled]="b !== ''"
          (input)="onInput($event, $index)"
          (keydown)="onKeydown($event, $index)"
          (paste)="onPaste($event)"
          (focus)="onFocus($event)"
        />
      }
    </div>
  `,
})
export class OtpInputComponent {
  length = input(6);
  codeChange = output<string>();
  boxes = signal<string[]>(['', '', '', '', '', '']);
  private inputs = viewChildren<ElementRef<HTMLInputElement>>('otpBox');

  constructor() {
    afterNextRender(() => this.focusBox(0));
    effect(() => {
      const n = this.length();
      if (this.boxes().length !== n) this.boxes.set(Array(n).fill(''));
    });
  }

  value(): string {
    return this.boxes().join('');
  }

  setValue(code: string) {
    const digits = (code ?? '').replace(/\D/g, '').slice(0, this.length()).split('');
    const next = Array(this.length()).fill('');
    digits.forEach((d, i) => (next[i] = d));
    this.boxes.set(next);
    this.codeChange.emit(next.join(''));
  }

  onInput(e: Event, idx: number) {
    const el = e.target as HTMLInputElement;
    let v = el.value.replace(/\D/g, '');
    // Playwright fill() puts the whole code into the first box -> distribute
    if (v.length > 1) {
      this.setValue(v);
      this.focusBox(Math.min(v.length, this.length() - 1));
      return;
    }
    const next = [...this.boxes()];
    next[idx] = v.slice(-1);
    this.boxes.set(next);
    this.codeChange.emit(next.join(''));
    if (v && idx < this.length() - 1) this.focusBox(idx + 1);
  }

  onKeydown(e: KeyboardEvent, idx: number) {
    const el = e.target as HTMLInputElement;
    if (e.key === 'Backspace' && !el.value && idx > 0) {
      e.preventDefault();
      const next = [...this.boxes()];
      next[idx - 1] = '';
      this.boxes.set(next);
      this.codeChange.emit(next.join(''));
      this.focusBox(idx - 1);
    } else if (e.key === 'ArrowLeft' && idx > 0) {
      e.preventDefault();
      this.focusBox(idx - 1);
    } else if (e.key === 'ArrowRight' && idx < this.length() - 1) {
      e.preventDefault();
      this.focusBox(idx + 1);
    }
  }

  onPaste(e: ClipboardEvent) {
    e.preventDefault();
    const text = e.clipboardData?.getData('text') ?? '';
    if (text) {
      this.setValue(text);
      this.focusBox(Math.min(text.replace(/\D/g, '').length, this.length() - 1));
    }
  }

  onFocus(e: Event) {
    (e.target as HTMLInputElement | null)?.select();
  }

  private focusBox(i: number) {
    const list = this.inputs();
    list[i]?.nativeElement.focus();
  }
}
