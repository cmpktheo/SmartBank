import { Component, input, output, signal, ElementRef, inject, computed } from '@angular/core';
import { LucideWallet, LucideChevronDown, LucideCheck } from '@lucide/angular';

export interface ComboOption {
  value: string;
  label: string;
  sub?: string;
  icon?: string;
}

/**
 * Professional combo (custom select) — ports UI.html `.combo` presentation
 * and behavior: toggle button + blurred panel + head/options/check + foot,
 * outside-click + Escape to close, ArrowUp/Down + Enter keyboard nav with
 * `.highlight`, aria-haspopup/listbox/option/selected/expanded.
 */
@Component({
  selector: 'sb-combo-box',
  standalone: true,
  imports: [LucideWallet, LucideChevronDown, LucideCheck],
  template: `
    <div class="combo" [class.open]="open()" [attr.data-testid]="comboTestid()">
      <button type="button" class="combo-btn" [style.width]="fullWidth() ? '100%' : null"
        [attr.data-testid]="buttonTestid()" aria-haspopup="listbox" [attr.aria-expanded]="open()"
        [attr.aria-label]="ariaLabel()" (click)="toggle($event)">
        @if (selectedOption(); as sel) {
          <span class="combo-ico" aria-hidden="true"><svg lucideWallet style="width:14px;height:14px" /></span>
          <span style="text-align:left;flex:1;min-width:0">
            <span class="combo-label" style="white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{{ sel.label }}</span>
            @if (sel.sub) { <span class="combo-sub" style="white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{{ sel.sub }}</span> }
          </span>
        } @else {
          <span class="muted" style="font-size:12px">{{ placeholder() }}</span>
        }
        <svg lucideChevronDown style="width:14px;height:14px" />
      </button>
      @if (open()) {
        <div class="combo-panel stretch" role="listbox" [attr.aria-label]="ariaLabel()">
          @if (heading()) { <div class="combo-head">{{ heading() }}</div> }
          @for (o of options(); track o.value) {
            <button type="button" role="option" [attr.aria-selected]="o.value === value()"
              [attr.data-value]="o.value" [attr.data-testid]="optionTestidPrefix() + o.value"
              class="combo-option" [class.selected]="o.value === value()"
              [class.highlight]="o.value === highlightValue()"
              (click)="pick(o.value, $event)" (mouseenter)="highlightValue.set(o.value)">
<span class="combo-ico" aria-hidden="true"><svg lucideWallet style="width:14px;height:14px" /></span>
               <span style="min-width:0"><span class="combo-label">{{ o.label }}</span>
                 @if (o.sub) { <span class="combo-sub">{{ o.sub }}</span> }</span>
               <span class="combo-check" aria-hidden="true"><svg lucideCheck style="width:14px;height:14px" /></span>
            </button>
          }
          @if (footer()) { <div class="combo-foot">{{ footer() }}</div> }
        </div>
      }
    </div>
  `,
})
export class ComboBoxComponent {
  private host = inject(ElementRef);

  options = input<ComboOption[]>([]);
  value = input<string>('');
  valueChange = output<string>();
  heading = input<string>('');
  footer = input<string>('');
  placeholder = input<string>('Select…');
  ariaLabel = input<string>('Options');
  comboTestid = input<string | null>(null);
  buttonTestid = input<string | null>(null);
  optionTestidPrefix = input<string>('');
  fullWidth = input(true);

  open = signal(false);
  highlightValue = signal<string>('');

  selectedOption = computed(() => this.options().find((o) => o.value === this.value()) ?? null);

  constructor() {
    document.addEventListener('click', (e) => {
      if (!this.host.nativeElement.contains(e.target)) this.open.set(false);
    });
    document.addEventListener('keydown', (e) => {
      if (!this.open()) return;
      if (e.key === 'Escape') { this.open.set(false); return; }
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Enter') {
        // Only hijack keys when focus is inside this combo
        if (!this.host.nativeElement.contains(document.activeElement)) return;
        e.preventDefault();
        const opts = this.options();
        if (!opts.length) return;
        let idx = opts.findIndex((o) => o.value === this.highlightValue());
        if (idx < 0) idx = opts.findIndex((o) => o.value === this.value());
        if (e.key === 'ArrowDown') idx = Math.min(opts.length - 1, idx + 1);
        if (e.key === 'ArrowUp') idx = Math.max(0, idx - 1 < 0 ? 0 : idx - 1);
        const next = opts[Math.max(0, idx)];
        if (next) this.highlightValue.set(next.value);
        if (e.key === 'Enter' && next) this.pick(next.value);
      }
    });
  }

  toggle(e?: Event) {
    e?.stopPropagation();
    const willOpen = !this.open();
    this.open.set(willOpen);
    if (willOpen) {
      this.highlightValue.set(this.value() || this.options()[0]?.value || '');
      // Focus the button so Arrow/Enter keys are scoped to this combo
      requestAnimationFrame(() => this.host.nativeElement.querySelector('.combo-btn')?.focus());
    }
  }

  pick(v: string, e?: Event) {
    e?.stopPropagation();
    this.valueChange.emit(v);
    this.open.set(false);
  }
}
